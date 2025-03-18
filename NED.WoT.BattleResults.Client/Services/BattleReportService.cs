using MudBlazor;

using NED.WoT.BattleResults.Client.Models;

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NED.WoT.BattleResults.Client.Services;

public partial class BattleReportService
{
    private const string SEARCH_PATTERN = "*.wotreplay";

    private readonly ISnackbar _snackbar;
    private readonly SettingService _settingService;
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private (string, string)? _clanMatch;
    private static ReadOnlySpan<byte> ReplayIdentifier => "\"clientVersionFromXml\""u8;
    private static ReadOnlySpan<byte> StatsIdentifier => "\"personal\""u8;

    public ConcurrentDictionary<string, BattleReport> BattleReports { get; set; } = new ConcurrentDictionary<string, BattleReport>();

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

        LoadingBattleReportsStarted?.Invoke(this, new EventArgs());

        DirectoryInfo directoryInfo = new(_settingService.Settings.WotReplayDirectory);
        List<FileInfo> files = directoryInfo
            .EnumerateFiles(SEARCH_PATTERN)
            .Where(x => x.LastWriteTime > _settingService.Settings.LoadBattlesSince && !IsTempFile(x.Name))
            .OrderByDescending(x => x.CreationTime)
            .ToList();

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
                BattleReport report = new()
                {
                    MatchStart = file.CreationTime,
                    Error = $"Failed to process file {file.Name}: {ex.Message}"
                };
            }
        });

        LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
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
        _timer = new Timer(async (state) =>
        {
            FileInfo tempFile = new(Path.Combine(_settingService.Settings.WotReplayDirectory, "test", "temp.wotreplay"));
            if (tempFile.Exists)
            {
                int currentLine = 0;

                (string, string)? clanMatch = null;

                using (FileStream fs = new(tempFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new(fs))
                {
                    while (!reader.EndOfStream)
                    {
                        string? line = await reader.ReadLineAsync();

                        currentLine++;
                        if (currentLine < 6)
                        {
                            continue;
                        }

                        var clan1Line = await reader.ReadLineAsync() ?? string.Empty;
                        string clan1 = ClanMatchRegex().Match(clan1Line)?.Value ?? string.Empty;
                        var clan2Line = await reader.ReadLineAsync() ?? string.Empty;
                        string clan2 = ClanMatchRegex().Match(clan2Line)?.Value ?? string.Empty;
                        clanMatch = (clan1, clan2);
                        break;
                    }
                }

                if (!_clanMatch.Equals(clanMatch) && clanMatch.HasValue)
                {
                    _clanMatch = clanMatch;

                    _snackbar.Add($"Match gestart tussen {_clanMatch.Value.Item1} en {_clanMatch.Value.Item2}", Severity.Info);
                    ClanMatchStarted?.Invoke(this, clanMatch.Value);
                }
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
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
        if (report != null)
        {
            AddBattleReport(e.Name, report);
            _snackbar.Add($"Bestand '{e.Name}' is toegevoegd", Severity.Success);
        }
        else
        {
            _snackbar.Add($"Kan bestand {e.Name} niet toevoegen", Severity.Warning);
        }

        LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (BattleReports.Remove(e.Name!, out BattleReport? report))
        {
            _snackbar.Add($"Replay bestand '{e.Name}' is verwijderd", Severity.Warning);
            BattleReportRemoved?.Invoke(this, new BattleReportRemovedEventArgs(report));
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _snackbar.Add($"Er is iets fout gegaan bij de file watcher: {e.GetException()}", Severity.Error);
    }

    private BattleReport GetBattleReport(FileInfo file)
    {
        BattleReport report = new();

        try
        {
            ReadOnlySpan<byte> fileData = File.ReadAllBytes(file.FullName);

            int replayStartIndex = fileData.IndexOf(ReplayIdentifier);
            JsonObject? replay = GetJsonFromFile<JsonObject>(fileData, '{', '}', replayStartIndex);
            if (replay?["dateTime"] == null)
            {
                report.MatchStart = file.CreationTime;
                report.Error = $"Could not parse file: {file.Name}";
            }
            else
            {
                int statsStartIndex = fileData.IndexOf(StatsIdentifier);
                JsonArray? stats = GetJsonFromFile<JsonArray>(fileData, '[', ']', statsStartIndex);
                report = BattleReportMapper.Map(replay, stats, _settingService.Settings);
            }
        }
        catch (Exception ex)
        {
            report.MatchStart = file.CreationTime;
            report.Error = ex.Message;
        }

        report.FileName = file.Name;

        return report;
    }

    private void AddBattleReport(string name, BattleReport report, bool notify = true)
    {
        if (!BattleReports.TryAdd(name, report))
        {
            _snackbar.Add($"Kan bestand '{name}' niet toevoegen", Severity.Error);
        }

        if (notify)
        {
            _clanMatch = null;
            BattleReportAdded?.Invoke(this, new BattleReportAddedEventArgs(report));
        }
    }

    private static T? GetJsonFromFile<T>(ReadOnlySpan<byte> fileData, char start, char end, int identifyIndex)
    {
        if (identifyIndex == -1)
        {
            return default;
        }

        try
        {
            int startIndex = 0;
            for (int i = identifyIndex; i > 0; i--)
            {
                if (fileData[i] == start)
                {
                    startIndex = i;
                    break;
                }
            }

            int jsonStarts = 0;
            int jsonEnds = 0;
            int length = fileData.Length;

            for (int i = startIndex; i < length; i++)
            {
                if (fileData[i] == start)
                {
                    jsonStarts++;
                }
                else if (fileData[i] == end && jsonStarts > jsonEnds)
                {
                    jsonEnds++;
                }

                if (jsonStarts > 0 && jsonStarts == jsonEnds)
                {
                    ReadOnlySpan<byte> jsonSpan = fileData.Slice(startIndex, i - startIndex + 1);
                    return JsonSerializer.Deserialize<T>(jsonSpan, _options);
                }
            }
        }
        catch
        {
            return default;
        }

        return default;
    }

    private static bool IsTempFile(string fileName)
    {
        return fileName.Contains("temp");
    }

    [GeneratedRegex(@"(?<=[\u0000-\uFFFF])[A-Z0-9-_]+(?=\s|[^A-Z0-9-])")]
    private static partial Regex ClanMatchRegex();
}