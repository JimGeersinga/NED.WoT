using Microsoft.AspNetCore.Components;

using MudBlazor;

using NED.WoT.BattleResults.Client.Models;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NED.WoT.BattleResults.Client.Services;

public partial class BattleReportService : IDisposable
{
    private const string SEARCH_PATTERN = "*.wotreplay";

    private readonly ISnackbar _snackbar;
    private readonly SettingService _settingService;
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private (string, string)? _clanMatch;

    private int _filesToLoad;
    private int _filesLoaded;

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

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BattleReportService(ISnackbar snackbar, SettingService settingService)
    {
        _snackbar = snackbar;
        _settingService = settingService;
        ListenForFileChanges();
    }

    public void LoadBattleReports()
    {
        if (!Directory.Exists(_settingService.Settings.WotReplayDirectory))
        {
            return;
        }

        Interlocked.Exchange(ref _filesToLoad, 0);
        Interlocked.Exchange(ref _filesLoaded, 0);
#if DEBUG
        ReplayReader.ResetBytesRead();
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif
        LoadingBattleReportsStarted?.Invoke(this, new EventArgs());

        DirectoryInfo directoryInfo = new(_settingService.Settings.WotReplayDirectory);
        List<FileInfo> files = directoryInfo
            .EnumerateFiles(SEARCH_PATTERN)
            .Where(x => x.LastWriteTime > _settingService.Settings.LoadBattlesSince && !IsTempFile(x.Name))
            .OrderByDescending(x => x.CreationTime)
            .ToList();
        Interlocked.Exchange(ref _filesToLoad, files.Count);

        OrderablePartitioner<FileInfo> partitioner = Partitioner.Create(files);
        Parallel.ForEach(partitioner, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
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

        LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
#if DEBUG
        Debug.WriteLine($"Battle list loaded: {files.Count} files, {ReplayReader.BytesRead / 1048576.0:F1} MB read, {stopwatch.ElapsedMilliseconds} ms");
#endif
        Debug.WriteLine($"Battle list loaded: managed heap {GC.GetTotalMemory(true) / 1048576} MB");
    }

    public void ListenForFileChanges()
    {
        if (!Directory.Exists(_settingService.Settings.WotReplayDirectory))
        {
            return;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(_settingService.Settings.WotReplayDirectory)
        {
            NotifyFilter = NotifyFilters.Attributes
                              | NotifyFilters.CreationTime
                              | NotifyFilters.FileName
                              | NotifyFilters.LastWrite
                              | NotifyFilters.Security
                              | NotifyFilters.Size,
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

        GC.SuppressFinalize(this);
    }

    private async Task TempFileWatcherAsync()
    {
        FileInfo tempFile = new(Path.Combine(_settingService.Settings.WotReplayDirectory ?? string.Empty, "temp.wotreplay"));
        if (tempFile.Exists)
        {
            (string, string)? clanMatch = null;

            using (FileStream fs = new(tempFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new(fs))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) is not null)
                {
                    Match clan1Match = ClanMatchRegex().Match(line ?? string.Empty);
                    if (clan1Match.Success)
                    {
                        line = await reader.ReadLineAsync();
                        Match clan2Match = ClanMatchRegex().Match(line ?? string.Empty);
                        if (clan2Match.Success)
                        {
                            clanMatch = (clan1Match.Value, clan2Match.Value);
                        }

                        break;
                    }
                }
            }

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
    }

    private void OnRenamed(object sender, RenamedEventArgs e) => OnCreated(sender, e);

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (e.Name is null || IsTempFile(e.Name))
        {
            return;
        }

        Task.Delay(1500).Wait();

        LoadingBattleReportsStarted?.Invoke(this, new EventArgs());

        BattleReport report = GetBattleReport(new FileInfo(e.FullPath));
        _clanMatch = null;

        // An unreadable replay is added too (it shows as a warning row); a duplicate has already had its own message.
        if (AddBattleReport(e.Name, report))
        {
            _snackbar.Add(
                report.Error == null ? Notice("Toegevoegd", e.Name) : Notice("Niet te lezen", e.Name),
                report.Error == null ? Severity.Success : Severity.Warning);
        }

        LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (BattleReports.Remove(e.Name!, out BattleReport? report))
        {
            _snackbar.Add(Notice("Verwijderd", $"{e.Name} is niet meer aanwezig"), Severity.Warning);
            BattleReportRemoved?.Invoke(this, new BattleReportRemovedEventArgs(report));
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Debug.WriteLine($"File watcher: {e.GetException()}");
        _snackbar.Add(Notice("Bestandsbewaking", "er ging iets mis"), Severity.Error);
    }

    private BattleReport GetBattleReport(FileInfo file)
    {
        BattleReport report;

        try
        {
            ReplayReader.ReplayBlocks blocks = ReplayReader.Read(file.FullName);

            JsonObject? replay = JsonNode.Parse(blocks.Header) as JsonObject;
            if (replay?["dateTime"] == null)
            {
                return Unreadable(file, "no dateTime in the replay header");
            }

            JsonArray? stats = blocks.Results is null ? null : JsonNode.Parse(blocks.Results) as JsonArray;
            report = BattleReportMapper.Map(replay, stats, _settingService.Settings);
        }
        catch (Exception ex)
        {
            return Unreadable(file, ex.Message);
        }

        report.FileName = file.Name;

        return report;
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

    /// <returns>False when a report with this file name is already there (the user has been told).</returns>
    private bool AddBattleReport(string name, BattleReport report, bool notify = true)
    {
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

    [GeneratedRegex(@"(?<=U[\u0000-\uFFFF])[A-Z0-9-_]+(?=s)")]
    private static partial Regex ClanMatchRegex();
}