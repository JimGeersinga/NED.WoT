using Microsoft.AspNetCore.Components;

using MudBlazor;

using NED.WoT.BattleResults.Client.Models;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace NED.WoT.BattleResults.Client.Services;

public class BattleReportService : IDisposable
{
    private const string SEARCH_PATTERN = "*.wotreplay";

    private readonly ISnackbar _snackbar;
    private readonly SettingService _settingService;
    private readonly IdentityService _identity;
    private FileSystemWatcher? _watcher;
    private readonly Timer _timer;
    private (string, string)? _clanMatch;
    private readonly SemaphoreSlim _tempFileGate = new(1);
    private readonly CancellationTokenSource _disposed = new();

    /// <summary>Cancels the load and the batch of the current folder; a switch cancels it and makes a new one. Linked to <see cref="_disposed"/>.</summary>
    private CancellationTokenSource _folderCts;
    private readonly SemaphoreSlim _loadGate = new(1);
    private readonly SemaphoreSlim _selectGate = new(1);

    /// <summary>The position of the folder that is read and watched, in <see cref="Settings.ReplayFolders"/>. The app starts on the first one.</summary>
    public int CurrentFolder { get; private set; }

    private string? ReplayDirectory => _settingService.Settings.ReplayFolders.ElementAtOrDefault(CurrentFolder)?.Path;

    /// <summary>New replays (name → path) waiting for the batch: it runs once no new file has arrived for 1 s.</summary>
    private readonly ConcurrentDictionary<string, string> _pending = new();
    private readonly Timer _batchTimer;
    private readonly SemaphoreSlim _batchGate = new(1);

    private int _filesToLoad;
    private int _filesLoaded;
    private long _bytesRead;

    /// <summary>The battles of the current folder: the only folder held in memory. A switch replaces it with an empty one.</summary>
    public ConcurrentDictionary<string, BattleReport> BattleReports { get; private set; } = new();

    private volatile bool _loaded;

    /// <returns>The readable battles of the current folder once its read has finished; null for every other folder, and while it is being read.</returns>
    public int? GetBattleCount(ReplayFolder folder) => _loaded && string.Equals(folder.Path, ReplayDirectory, StringComparison.OrdinalIgnoreCase)
        ? BattleReports.Values.Count(x => x.Error == null)
        : null;

    /// <summary>The current folder has been selected: its battles are gone and its read starts next.</summary>
    public event EventHandler? FolderChanged;

    /// <summary>The replay files the current load has to read (0 until the folder is listed): the "n van m bestanden" of the progress bar.</summary>
    public int FilesToLoad => Volatile.Read(ref _filesToLoad);

    /// <summary>The replay files the current load has read so far, readable or not.</summary>
    public int FilesLoaded => Volatile.Read(ref _filesLoaded);

    public event EventHandler? LoadingBattleReportsStarted;
    public event EventHandler? LoadingBattleReportsFinished;
    public event EventHandler<BattleReportAddedEventArgs>? BattleReportAdded;
    public event EventHandler<BattleReportRemovedEventArgs>? BattleReportRemoved;
    public event EventHandler<(string, string)>? ClanMatchStarted;

    public BattleReportService(ISnackbar snackbar, SettingService settingService, IdentityService identity)
    {
        _snackbar = snackbar;
        _settingService = settingService;
        _identity = identity;
        _folderCts = CancellationTokenSource.CreateLinkedTokenSource(_disposed.Token);
        _batchTimer = new Timer(async _ => await AddPendingFilesAsync(), null, Timeout.Infinite, Timeout.Infinite);
        SelfCheck();
        // The live lookup runs whichever folder is selected; every tick reads the setting, so a changed path needs no restart.
        _timer = new Timer(async _ => await TempFileWatcherAsync(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        ListenForFileChanges();
    }

    /// <summary>
    /// Makes the folder at <paramref name="index"/> the current one: stops what the old one was doing, drops its battles, watches the new folder and reads it from disk.
    /// Every switch reads again, also back to a folder seen before. A second call while this one loads cancels that load.
    /// </summary>
    public async Task SelectFolderAsync(int index)
    {
        if (index < 0 || index >= _settingService.Settings.ReplayFolders.Count || index == CurrentFolder || _disposed.IsCancellationRequested)
        {
            return;
        }

        // The load of the old folder stops at once, so a quick second click does not wait for it.
        _folderCts.Cancel();
        try
        {
            await _selectGate.WaitAsync(_disposed.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            await _loadGate.WaitAsync(_disposed.Token);
            _loadGate.Release();

            await _batchGate.WaitAsync(_disposed.Token);
            try
            {
                StopWatching();
                _pending.Clear();
                _folderCts.Dispose();
                _folderCts = CancellationTokenSource.CreateLinkedTokenSource(_disposed.Token);
                CurrentFolder = index;
                BattleReports = new ConcurrentDictionary<string, BattleReport>();
                _loaded = false;
            }
            finally
            {
                _batchGate.Release();
            }

            // Watching first, as at startup: a replay saved while the folder is being read is queued, not lost.
            ListenForFileChanges();
            FolderChanged?.Invoke(this, EventArgs.Empty);

            if (Directory.Exists(ReplayDirectory))
            {
                await Task.Run(LoadBattleReports);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed during the switch.
        }
        finally
        {
            _selectGate.Release();
        }
    }

    public void LoadBattleReports()
    {
        if (!Directory.Exists(ReplayDirectory))
        {
            return;
        }

        // A switch cancels this load and waits for the gate before it changes the folder, so the folder and the battles of this load stay the same all through it.
        _loadGate.Wait();
        try
        {
            // A switch may have read this folder already (Index's start-up load can arrive after it).
            if (!_loaded)
            {
                LoadCurrentFolder();
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private void LoadCurrentFolder()
    {
        ConcurrentDictionary<string, BattleReport> cache = BattleReports;
        CancellationToken cancellation = _folderCts.Token;

        Interlocked.Exchange(ref _filesToLoad, 0);
        Interlocked.Exchange(ref _filesLoaded, 0);
        Interlocked.Exchange(ref _bytesRead, 0);
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        Stopwatch stopwatch = Stopwatch.StartNew();
        LoadingBattleReportsStarted?.Invoke(this, new EventArgs());

        List<FileInfo> files = ReplayFiles();
        Interlocked.Exchange(ref _filesToLoad, files.Count);

        OrderablePartitioner<FileInfo> partitioner = Partitioner.Create(files);
        try
        {
            Parallel.ForEach(partitioner, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellation }, file =>
            {
                try
                {
                    BattleReport report = GetBattleReport(file);

                    AddBattleReport(cache, file.Name, report, _settingService.Settings.UpdateScreeenWhileLoading);
                }
                catch (Exception ex)
                {
                    // The unreadable file becomes a row of its own in the table, not a silent gap.
                    AddBattleReport(cache, file.Name, Unreadable(file, ex.Message), _settingService.Settings.UpdateScreeenWhileLoading);
                }
                finally
                {
                    Interlocked.Increment(ref _filesLoaded);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Disposed, or another folder selected, during the load: this load is over (a switch replaces what it read).
            Debug.WriteLine($"Battle list load cancelled after {_filesLoaded} of {files.Count} files");

            return;
        }

        _loaded = true;

        long elapsedMs = stopwatch.ElapsedMilliseconds;
        long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
        Debug.WriteLine($"Battle list loaded: {files.Count} files, {Interlocked.Read(ref _bytesRead) / 1048576.0:F1} MB read, {allocated / 1048576.0:F1} MB allocated, {elapsedMs} ms");
        Debug.WriteLine($"Battle list loaded: managed heap {GC.GetTotalMemory(true) / 1048576} MB");
    }

    public void ListenForFileChanges()
    {
        StopWatching();
        if (!Directory.Exists(ReplayDirectory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(ReplayDirectory)
        {
            // Created, Renamed and Deleted need only the file name; every other filter adds events per copied file.
            NotifyFilter = NotifyFilters.FileName,
            InternalBufferSize = 65536,
            Filter = SEARCH_PATTERN,
            EnableRaisingEvents = true,
            IncludeSubdirectories = false
        };

        _watcher.Created += OnCreated;
        _watcher.Renamed += OnRenamed;
        _watcher.Deleted += OnDeleted;
        _watcher.Error += OnError;
    }

    /// <summary>Stops the watcher of the folder that is left (or of one that does not exist any more).</summary>
    private void StopWatching()
    {
        if (_watcher is not null)
        {
            _watcher.Created -= OnCreated;
            _watcher.Renamed -= OnRenamed;
            _watcher.Deleted -= OnDeleted;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    /// <summary>A reload makes a new DI scope; the scope disposes this one, so the old timer and watcher stop and the old battle list can be collected.</summary>
    public void Dispose()
    {
        _disposed.Cancel();
        StopWatching();

        _timer.Dispose();
        _batchTimer.Dispose();
        _pending.Clear();

        GC.SuppressFinalize(this);
    }

    /// <returns>The configured temp.wotreplay (a configured folder gives temp.wotreplay in it), else temp.wotreplay in the first replay folder; null when neither is known.</returns>
    public static string? LiveReplayFile(string? configured, string? firstFolder)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Directory.Exists(configured) ? Path.Combine(configured, "temp.wotreplay") : configured;
        }

        return string.IsNullOrWhiteSpace(firstFolder) ? null : Path.Combine(firstFolder, "temp.wotreplay");
    }

    [Conditional("DEBUG")]
    private static void SelfCheck()
    {
        Debug.Assert(LiveReplayFile(@"D:\Live\temp.wotreplay", @"C:\replays") == @"D:\Live\temp.wotreplay", "a set path wins");
        Debug.Assert(LiveReplayFile("  ", @"C:\replays") == @"C:\replays\temp.wotreplay", "empty falls back to the first folder");
        string folder = Path.GetTempPath();
        Debug.Assert(LiveReplayFile(folder, @"C:\replays") == Path.Combine(folder, "temp.wotreplay"), "a configured folder gives temp.wotreplay in it");
        Debug.Assert(LiveReplayFile(null, null) is null, "nothing known: no lookup");
    }

    /// <summary>Every 2 s: the two clan tags of the battle being played, from block 0 of temp.wotreplay. A tick that finds the previous one still running is skipped.</summary>
    private Task TempFileWatcherAsync()
    {
        if (!_tempFileGate.Wait(0))
        {
            return Task.CompletedTask;
        }

        try
        {
            string? tempFile = LiveReplayFile(_settingService.Settings.LiveReplayPath, _settingService.Settings.ReplayFolders.FirstOrDefault()?.Path);
            if (tempFile is null || !File.Exists(tempFile))
            {
                // temp.wotreplay exists only during a battle: once it is gone the next battle's match is new again.
                _clanMatch = null;
                return Task.CompletedTask;
            }

            ReplayHeader? header = JsonSerializer.Deserialize(ReplayReader.Read(tempFile).Header, ReplayJsonContext.Default.ReplayHeader);
            (string, string)? clanMatch = ClanTags(header);

            if (!_clanMatch.Equals(clanMatch) && clanMatch.HasValue &&
                _identity.Current?.ClanTag is { } myClan && (
                myClan.Equals(clanMatch.Value.Item1, StringComparison.OrdinalIgnoreCase) ||
                myClan.Equals(clanMatch.Value.Item2, StringComparison.OrdinalIgnoreCase)
                ))
            {
                _clanMatch = clanMatch;

                _snackbar.Add(Notice("Match gestart", $"{_clanMatch.Value.Item1} tegen {_clanMatch.Value.Item2}"), Severity.Info);
                ClanMatchStarted?.Invoke(this, clanMatch.Value);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            // The game is still writing the header, or the file is gone: the next tick tries again.
            Debug.WriteLine($"temp.wotreplay not read: {ex.Message}");
        }
        finally
        {
            _tempFileGate.Release();
        }

        return Task.CompletedTask;
    }

    /// <returns>The clan tags of team 1 and team 2 (the most mentioned clanAbbrev per team, as in the battle list), or null when a team has none.</returns>
    private static (string, string)? ClanTags(ReplayHeader? header)
    {
        if (header?.Vehicles is null)
        {
            return null;
        }

        Team team1 = new() { Number = 1 };
        Team team2 = new() { Number = 2 };
        foreach (ReplayPlayer player in header.Vehicles.Values)
        {
            // The same split as BattleReportMapper.MapPlayerData: team 1, everyone else team 2.
            Team team = player.Team == team1.Number ? team1 : team2;
            team.Players.Add(new Player(team) { Clan = player.ClanAbbrev });
        }

        string clan1 = BattleReportMapper.GetMostMentionedClanAbbreviation(team1);
        string clan2 = BattleReportMapper.GetMostMentionedClanAbbreviation(team2);

        return clan1 == "?" || clan2 == "?" ? null : (clan1, clan2);
    }

    private void OnRenamed(object sender, RenamedEventArgs e) => OnCreated(sender, e);

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (e.Name is null || IsTempFile(e.Name))
        {
            return;
        }

        QueueNewFile(e.Name, e.FullPath);
    }

    /// <summary>Puts a new replay in the pending set (unless it is listed or pending already) and restarts the 1 s quiet timer of the batch.</summary>
    private void QueueNewFile(string name, string fullPath)
    {
        if (_disposed.IsCancellationRequested || BattleReports.ContainsKey(name) || !_pending.TryAdd(name, fullPath))
        {
            return;
        }

        try
        {
            _batchTimer.Change(1000, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
            // Disposed between the check and here: the new service lists the folder itself.
        }
    }

    /// <summary>The batch timer fired: adds everything that is pending and not listed yet as one batch. One batch at a time: a tick during a batch waits for it, then drains what is pending by then.</summary>
    private async Task AddPendingFilesAsync()
    {
        await _batchGate.WaitAsync();
        try
        {
            // A snapshot: a name leaves _pending only once it is listed (see AddNewFilesAsync), so a file in flight still blocks QueueNewFile.
            // The battles and the token of the folder this batch is for: a switch waits for the gate before it changes them.
            ConcurrentDictionary<string, BattleReport> cache = BattleReports;
            CancellationToken token = _folderCts.Token;
            List<KeyValuePair<string, string>> batch = [];
            foreach (KeyValuePair<string, string> file in _pending)
            {
                if (cache.ContainsKey(file.Key))
                {
                    _pending.TryRemove(file.Key, out _);
                }
                else
                {
                    batch.Add(file);
                }
            }

            if (batch.Count > 0 && !token.IsCancellationRequested)
            {
                await AddNewFilesAsync(batch, cache, token);
            }
        }
        finally
        {
            _batchGate.Release();
        }
    }

    /// <summary>Adds the replays that appeared in the folder: waits until the copier has finished with them, reads them like a load does and tells the user once. Never throws (it runs from a timer callback).</summary>
    private async Task AddNewFilesAsync(List<KeyValuePair<string, string>> batch, ConcurrentDictionary<string, BattleReport> cache, CancellationToken token)
    {
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // A file that stays locked is read anyway after 10 s; it then shows as an unreadable row.
            await Task.WhenAll(batch.Select(async file =>
            {
                try
                {
                    await WaitUntilWrittenAsync(file.Value, token);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"New replay {file.Key} still locked: {ex.Message}");
                }
            }));

            LoadingBattleReportsStarted?.Invoke(this, new EventArgs());
            _clanMatch = null;

            ConcurrentBag<string> added = [];
            int unreadable = 0;
            Parallel.ForEach(Partitioner.Create(batch), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token }, file =>
            {
                BattleReport report = GetBattleReport(new FileInfo(file.Value));

                // An unreadable replay is added too (it shows as a warning row).
                bool listed = AddBattleReport(cache, file.Key, report);
                _pending.TryRemove(file.Key, out _);
                if (listed)
                {
                    added.Add(file.Key);
                    if (report.Error != null)
                    {
                        Interlocked.Increment(ref unreadable);
                    }
                }
            });

            if (added.Count == 1)
            {
                string name = added.First();
                _snackbar.Add(unreadable == 0 ? Notice("Toegevoegd", name) : Notice("Niet te lezen", name), unreadable == 0 ? Severity.Success : Severity.Warning);
            }
            else if (added.Count > 1)
            {
                string sentence = unreadable == 0 ? $"{added.Count} replays" : $"{added.Count} replays, {unreadable} niet te lezen";
                _snackbar.Add(NoticeList("Toegevoegd", sentence, [.. added.Order(StringComparer.Ordinal)]), unreadable == 0 ? Severity.Success : Severity.Warning);
            }

            LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
            Debug.WriteLine($"New replays loaded: {batch.Count} files, {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (OperationCanceledException)
        {
            // Disposed, or another folder selected, during the batch: the new service, or the next visit of this folder, lists the folder itself.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"New replays not added: {ex.Message}");
            _snackbar.Add(Notice("Niet te lezen", batch.Count == 1 ? batch.First().Key : $"{batch.Count} replays"), Severity.Warning);
        }
    }

    /// <summary>Opening with FileShare.Read throws an IOException while the copier still has the file open for writing; retries every 250 ms for up to 10 s, then lets the last exception through.</summary>
    private static async Task WaitUntilWrittenAsync(string path, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

                return;
            }
            catch (IOException ex) when (ex is not FileNotFoundException && attempt < 40)
            {
                await Task.Delay(250, token);
            }
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (BattleReports.Remove(e.Name!, out BattleReport? report))
        {
            _snackbar.Add(Notice("Verwijderd", $"{e.Name} is niet meer aanwezig"), Severity.Warning);
            BattleReportRemoved?.Invoke(this, new BattleReportRemovedEventArgs(report));
        }
    }

    /// <summary>The watcher lost events (buffer overflow): list the folder again and add every replay that isn't in the list yet.</summary>
    private void OnError(object sender, ErrorEventArgs e)
    {
        Debug.WriteLine($"File watcher: {e.GetException()}");
        _snackbar.Add(Notice("Bestandsbewaking", "er ging iets mis"), Severity.Error);

        try
        {
            foreach (FileInfo file in ReplayFiles())
            {
                QueueNewFile(file.Name, file.FullName);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"File watcher rescan failed: {ex.Message}");
        }
    }

    /// <summary>The replays a load reads (the callers have checked that the folder is set): the folder's .wotreplay files, newest first, without temp.wotreplay.</summary>
    private List<FileInfo> ReplayFiles() => new DirectoryInfo(ReplayDirectory!)
        .EnumerateFiles(SEARCH_PATTERN)
        .Where(x => !IsTempFile(x.Name))
        .OrderByDescending(x => x.CreationTime)
        .ToList();

    private BattleReport GetBattleReport(FileInfo file)
    {
        BattleReport report;

        try
        {
            ReplayReader.ReplayBlocks blocks = ReplayReader.Read(file.FullName);
            Interlocked.Add(ref _bytesRead, blocks.Header.Length + (blocks.Results?.Length ?? 0));

            ReplayHeader? header = JsonSerializer.Deserialize(blocks.Header, ReplayJsonContext.Default.ReplayHeader);
            if (header?.DateTime == null)
            {
                return Unreadable(file, "no dateTime in the replay header");
            }

            ReplayResults? results = ParseResults(blocks.Results);
            report = BattleReportMapper.Map(header, results, _identity.Current);
        }
        catch (Exception ex)
        {
            return Unreadable(file, ex.Message);
        }

        report.FileName = file.Name;

        return report;
    }

    /// <summary>The results block; null when there is none or it isn't a valid results array (the battle then maps without stats).</summary>
    private static ReplayResults? ParseResults(byte[]? results)
    {
        if (results is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(results, ReplayJsonContext.Default.ReplayResults);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>What the warning row of an unreadable replay says.</summary>
    public static string UnreadableMessage(string fileName) => $"Kan bestand {fileName} niet lezen";

    /// <summary>The report of a replay that can't be read: no battle, only the warning row. The reason goes to the debug output, not to the UI.</summary>
    private static BattleReport Unreadable(FileInfo file, string reason)
    {
        Debug.WriteLine($"Unreadable replay {file.Name}: {reason}");

        return new BattleReport { FileName = file.Name, MatchStart = file.CreationTime, Error = UnreadableMessage(file.Name) };
    }

    /// <summary>A snackbar message as lead word · sentence (docs/DESIGN.md, "snackbar"): the lead word is bold in the severity colour. Both parts are HTML-encoded, so a file name is safe in it.</summary>
    public static MarkupString Notice(string lead, string sentence) => new($"<b>{WebUtility.HtmlEncode(lead)} · </b>{WebUtility.HtmlEncode(sentence)}");

    /// <summary>A batch snackbar: the <see cref="Notice"/> line, then the file names one per line (at most 10, then "en m meer"). All parts are HTML-encoded.</summary>
    public static MarkupString NoticeList(string lead, string sentence, IReadOnlyList<string> names)
    {
        const int MAX_NAMES = 10;

        string lines = string.Concat(names.Take(MAX_NAMES).Select(x => $"<br />{WebUtility.HtmlEncode(x)}"));
        string more = names.Count > MAX_NAMES ? $"<br />en {names.Count - MAX_NAMES} meer" : string.Empty;

        return new($"<b>{WebUtility.HtmlEncode(lead)} · </b>{WebUtility.HtmlEncode(sentence)}{lines}{more}");
    }

    /// <returns>False when a report with this file name is already there (the user has been told).</returns>
    private bool AddBattleReport(ConcurrentDictionary<string, BattleReport> cache, string name, BattleReport report, bool notify = true)
    {
        if (_disposed.IsCancellationRequested)
        {
            Debug.WriteLine($"Battle report not added, service disposed: {name}");

            return false;
        }

        bool added = cache.TryAdd(name, report);
        if (!added)
        {
            _snackbar.Add(Notice("Niet toegevoegd", $"{name} staat al in de lijst"), Severity.Warning);
        }

        if (notify)
        {
            BattleReportAdded?.Invoke(this, new BattleReportAddedEventArgs(report));
        }

        return added;
    }

    private static bool IsTempFile(string fileName)
    {
        return fileName.Contains("temp");
    }
}