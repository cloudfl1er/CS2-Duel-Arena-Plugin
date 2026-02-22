using CounterStrikeSharp.API;

namespace CS2DuelServer;

public class DemoRecorder
{
    private readonly PluginConfig _config;
    private bool _isRecording;
    private string _currentDemoFile = "";

    public DemoRecorder(PluginConfig config)
    {
        _config = config;
    }

    public string StartRecording(int matchId)
    {
        if (!_config.EnableDemoRecording) return "";

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filename = $"duel_{matchId}_{timestamp}.dem";
        var demoPath = Path.Combine(_config.DemoFolder, filename);

        Directory.CreateDirectory(_config.DemoFolder);

        Server.ExecuteCommand($"tv_record \"{demoPath}\"");
        _isRecording = true;
        _currentDemoFile = demoPath;

        if (_config.DebugMode)
            Console.WriteLine($"[CS2DuelServer] Demo recording started: {demoPath}");

        return demoPath;
    }

    public string StopRecording()
    {
        if (!_isRecording) return _currentDemoFile;

        Server.ExecuteCommand("tv_stoprecord");
        _isRecording = false;

        Console.WriteLine($"[CS2DuelServer] Demo recording stopped: {_currentDemoFile}");

        return _currentDemoFile;
    }

    public bool IsRecording => _isRecording;
    public string CurrentDemoFile => _currentDemoFile;
}
