using MudBlazor;

using NED.WoT.BattleResults.Client.Data;
using NED.WoT.BattleResults.Client.Models;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NED.WoT.BattleResults.Client.Services;

public class BattleReportService
{
    private const string SEARCH_PATTERN = "*.wotreplay";

    private readonly ISnackbar _snackbar;
    private readonly SettingService _settingService;
    private FileSystemWatcher _watcher;

    public ConcurrentDictionary<string, BattleReport> BattleReports { get; set; } = new ConcurrentDictionary<string, BattleReport>();

    public event EventHandler LoadingBattleReportsStarted;
    public event EventHandler LoadingBattleReportsFinished;
    public event EventHandler<BattleReportAddedEventArgs> BattleReportAdded;
    public event EventHandler<BattleReportRemovedEventArgs> BattleReportRemoved;


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

        var directoryInfo = new DirectoryInfo(_settingService.Settings.WotReplayDirectory);
        var files = directoryInfo.GetFiles(SEARCH_PATTERN)
            .Where(x => x.LastWriteTime > _settingService.Settings.LoadBattlesSince)
            .Where(x => !IsTempFile(x.Name))
            .OrderByDescending(x => x.CreationTime);

        Parallel.ForEach(files, (file) =>
        {
            var report = GetBattleReport(file);
            AddBattleReport(file.Name, report);
        });

        LoadingBattleReportsFinished?.Invoke(this, new EventArgs());
    }

    public void ListenForFileChanges()
    {
        if (!Directory.Exists(_settingService.Settings.WotReplayDirectory))
        {
            return;
        }
        else if (_watcher != null)
        {
            _watcher.Dispose();
        }

        _watcher = new FileSystemWatcher(_settingService.Settings.WotReplayDirectory)
        {
            NotifyFilter = NotifyFilters.Attributes
                              | NotifyFilters.CreationTime
                              | NotifyFilters.DirectoryName
                              | NotifyFilters.FileName
                              | NotifyFilters.LastAccess
                              | NotifyFilters.LastWrite
                              | NotifyFilters.Security
                              | NotifyFilters.Size,
            Filter = SEARCH_PATTERN,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnCreated;
        _watcher.Renamed += OnRenamed;
        _watcher.Deleted += OnDeleted;
        _watcher.Error += OnError;
    }

    private void OnRenamed(object sender, RenamedEventArgs e) => OnCreated(sender, e);
    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsTempFile(e.Name))
        {
            return;
        }

        Task.Delay(1500).Wait();

        LoadingBattleReportsStarted?.Invoke(this, new EventArgs());

        var report = GetBattleReport(new FileInfo(e.FullPath));
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
        if (BattleReports.Remove(e.Name, out var report))
        {
            _snackbar.Add($"Replay bestand '{e.Name}' is verwijderd", Severity.Warning);
            BattleReportRemoved?.Invoke(this, new BattleReportRemovedEventArgs(report));
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _snackbar.Add($"Er is iets fout gegaan bij de file watcher: {e.GetException()}", Severity.Error);
    }

    private static BattleReport GetBattleReport(FileInfo file)
    {
        BattleReport report = new();

        try
        {
            int startIndex = 0;
            byte[] fileData = File.ReadAllBytes(file.FullName);
            var replay = GetJsonFromFile<JsonObject>(fileData, '{', '}', ref startIndex);
            var stats = GetJsonFromFile<JsonArray>(fileData, '[', ']', ref startIndex);

            if (replay == null)
            {
                report.Error = $"Could not parse file: {file.Name}";
            }
            else
            {
                report = BattleReportMapper.Map(replay, stats);
            }
        }
        catch (Exception ex)
        {
            report.Error = ex.Message;
        }

        report.FileName = file.Name;

        return report;
    }

    private static T GetJsonFromFile<T>(byte[] fileData, char start, char end, ref int startIndex)
    {
        try
        {
            int jsonStarts = 0;
            int jsonEnds = 0;

            for (int i = startIndex; i < fileData.Length; i++)
            {
                if (fileData[i] == start)
                {
                    jsonStarts++;
                    if (jsonStarts == 1)
                    {
                        startIndex = i;
                    }
                }
                else if (fileData[i] == end)
                {
                    jsonEnds++;
                }

                if (jsonStarts > 0 && jsonStarts == jsonEnds)
                {
                    string json = Encoding.UTF8.GetString(fileData, startIndex, i - startIndex + 1);

                    // Update startindex for next json search
                    startIndex = i;

                    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
            }
        }
        catch (Exception)
        {
            return default;
        }

        return default;
    }

    private static bool IsTempFile(string fileName)
    {
        return fileName.Contains("temp");
    }

    private void AddBattleReport(string name, BattleReport report)
    {
        if (!BattleReports.TryAdd(name, report))
        {
            _snackbar.Add($"Kan bestand '{name}' niet toevoegen", Severity.Error);
        }

        BattleReportAdded?.Invoke(this, new BattleReportAddedEventArgs(report));
    }

}

