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
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private (string, string)? _clanMatch;
    private readonly SemaphoreSlim _tempFileGate = new(1);
    private readonly CancellationTokenSource _disposed = new();

    /// <summary>The folder that is read and watched: the first replay folder, until #16 adds the selection.</summary>
    private string? ReplayDirectory => _settingService.Settings.ReplayFolders.FirstOrDefault()?.Path;

    /// <summary>New replays (name → path) waiting for the batch: it runs once no new file has arrived for 1 s.</summary>
    private readonly ConcurrentDictionary<string, string> _pending = new();
    private readonly Timer _batchTimer;
    private readonly SemaphoreSlim _batchGate = new(1);

    private int _filesToLoad;
    private int _filesLoaded;
    private long _bytesRead;

    public ConcurrentDictionary<string, BattleReport> BattleReports { get; set; } = new ConcurrentDictionary<string, BattleReport>();

    /// <summary>The replay files the current load has to read (0 until the folder is listed): the "n van m bestanden" of the progress bar.</summary>
    public int FilesToLoad => Volatile.Read(ref _filesToLoad);

    /// <summary>The replay files the current load has read so far, readable or not.</summary>
    public int FilesLoaded => Volatile.Read(ref _filesLoaded);

    public event EventHandler? LoadingBattleReportsStarted;
    public event EventHandler? LoadingBattleReportsFinished;
    public event EventHandler<BattleReportAddedEventArgs>? BattleReportAdded;
    public event EventHandler<BattleReportRemovedEventArgs>? BattleReportRemoved;
    public event EventHandler<(string, string)>? ClanMatchStarted;

    public BattleReportService(ISnackbar snackbar, SettingService settingService)
    {
        _snackbar = snackbar;
        _settingService = settingService;
        _batchTimer = new Timer(async _ => await AddPendingFilesAsync(), null, Timeout.Infinite, Timeout.Infinite);
        ListenForFileChanges();
    }

    public void LoadBattleReports()
    {
        if (!Directory.Exists(ReplayDirectory))
        {
            return;
        }

        Interlocked.Exchange(ref _filesToLoad, 0);
        Interlocked.Exchange(ref _filesLoaded, 0);
        Interlocked.Exchange(ref _bytesRead, 0);
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        Stopwatch stopwatch = Stopwatch.StartNew();
        LoadingBattleReportsStarted?.Invoke(this, new EventArgs());

        List<FileInfo> files = ReplayFiles();
        Interlocked.Exchange(ref _filesToLoad, files.Count);

        OrderablePartitioner<FileInfo> partitioner = Partitioner.Create(files);
        CancellationToken cancellation = _disposed.Token;
        try
        {
            Parallel.ForEach(partitioner, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellation }, file =>
            {
                try
                {
                    BattleReport report = GetBattleReport(file);

                    AddBattleReport(file.Name, report, _settingService.Settings.UpdateScreeenWhileLoading);
                }
                catch (Exception ex)
                {
                    // The unreadable file becomes a row of its own in the table, not a silent gap.
                    AddBattleReport(file.Name, Unreadable(file, ex.Message), _settingService.Settings.UpdateScreeenWhileLoading);
                }
                finally
                {
                    Interlocked.Increment(ref _filesLoaded);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Disposed during the load: this load is over, the new service runs its own.
            Debug.WriteLine($"Battle list load cancelled after {_filesLoaded} of {files.Count} files");

            return;
        }

        long elapsedMs = stopwatch.ElapsedMilliseconds;
        long allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
        Debug.WriteLine($"Battle list loaded: {files.Count} files, {Interlocked.Read(ref _bytesRead) / 1048576.0:F1} MB read, {allocated / 1048576.0:F1} MB allocated, {elapsedMs} ms");
        Debug.WriteLine($"Battle list loaded: managed heap {GC.GetTotalMemory(true) / 1048576} MB");
    }

    public void ListenForFileChanges()
    {
        if (!Directory.Exists(ReplayDirectory))
        {
            return;
        }

        _watcher?.Dispose();
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

        _timer?.Dispose();
        _timer = new Timer(async _ => await TempFileWatcherAsync(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    /// <summary>A reload makes a new DI scope; the scope disposes this one, so the old timer and watcher stop and the old battle list can be collected.</summary>
    public void Dispose()
    {
        _disposed.Cancel();
        _timer?.Dispose();
        BattleReports.Clear();

        if (_watcher is not null)
        {
            _watcher.Created -= OnCreated;
            _watcher.Renamed -= OnRenamed;
            _watcher.Deleted -= OnDeleted;
            _watcher.Error -= OnError;
            _watcher.Dispose();
        }

        _batchTimer.Dispose();
        _pending.Clear();

        GC.SuppressFinalize(this);
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
            string tempFile = Path.Combine(ReplayDirectory ?? string.Empty, "temp.wotreplay");
            if (!File.Exists(tempFile))
            {
                return Task.CompletedTask;
            }

            ReplayHeader? header = JsonSerializer.Deserialize(ReplayReader.Read(tempFile).Header, ReplayJsonContext.Default.ReplayHeader);
            (string, string)? clanMatch = ClanTags(header);

            if (!_clanMatch.Equals(clanMatch) && clanMatch.HasValue &&
                _settingService.Settings.ClanAbbreviation is not null && (
                _settingService.Settings.ClanAbbreviation.Equals(clanMatch.Value.Item1, StringComparison.OrdinalIgnoreCase) ||
                _settingService.Settings.ClanAbbreviation.Equals(clanMatch.Value.Item2, StringComparison.OrdinalIgnoreCase)
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
            List<KeyValuePair<string, string>> batch = [];
            foreach (KeyValuePair<string, string> file in _pending)
            {
                if (BattleReports.ContainsKey(file.Key))
                {
                    _pending.TryRemove(file.Key, out _);
                }
                else
                {
                    batch.Add(file);
                }
            }

            if (batch.Count > 0 && !_disposed.IsCancellationRequested)
            {
                await AddNewFilesAsync(batch);
            }
        }
        finally
        {
            _batchGate.Release();
        }
    }

    /// <summary>Adds the replays that appeared in the folder: waits until the copier has finished with them, reads them like a load does and tells the user once. Never throws (it runs from a timer callback).</summary>
    private async Task AddNewFilesAsync(List<KeyValuePair<string, string>> batch)
    {
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            // A file that stays locked is read anyway after 10 s; it then shows as an unreadable row.
            await Task.WhenAll(batch.Select(async file =>
            {
                try
                {
                    await WaitUntilWrittenAsync(file.Value);
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
            Parallel.ForEach(Partitioner.Create(batch), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = _disposed.Token }, file =>
            {
                BattleReport report = GetBattleReport(new FileInfo(file.Value));

                // An unreadable replay is added too (it shows as a warning row).
                bool listed = AddBattleReport(file.Key, report);
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
            // Disposed during the batch: the new service lists the folder itself.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"New replays not added: {ex.Message}");
            _snackbar.Add(Notice("Niet te lezen", batch.Count == 1 ? batch.First().Key : $"{batch.Count} replays"), Severity.Warning);
        }
    }

    /// <summary>Opening with FileShare.Read throws an IOException while the copier still has the file open for writing; retries every 250 ms for up to 10 s, then lets the last exception through.</summary>
    private async Task WaitUntilWrittenAsync(string path)
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
                await Task.Delay(250, _disposed.Token);
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

    /// <summary>The replays a load reads (the callers have checked that the folder is set): the folder's .wotreplay files since the start date, newest first, without temp.wotreplay.</summary>
    private List<FileInfo> ReplayFiles() => new DirectoryInfo(ReplayDirectory!)
        .EnumerateFiles(SEARCH_PATTERN)
        .Where(x => x.LastWriteTime > _settingService.Settings.LoadBattlesSince && !IsTempFile(x.Name))
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
            report = BattleReportMapper.Map(header, results, _settingService.Settings);
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
    private bool AddBattleReport(string name, BattleReport report, bool notify = true)
    {
        if (_disposed.IsCancellationRequested)
        {
            Debug.WriteLine($"Battle report not added, service disposed: {name}");

            return false;
        }

        bool added = BattleReports.TryAdd(name, report);
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