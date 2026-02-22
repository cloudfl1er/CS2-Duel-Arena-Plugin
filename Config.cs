using Newtonsoft.Json;

namespace CS2DuelServer;

public class PluginConfig
{
    [JsonProperty("server_id")]
    public int ServerId { get; set; } = 1;

    [JsonProperty("api_base_url")]
    public string ApiBaseUrl { get; set; } = "http://localhost:8000";

    [JsonProperty("api_key")]
    public string ApiKey { get; set; } = "YOUR_API_KEY_HERE";

    [JsonProperty("poll_interval_seconds")]
    public int PollIntervalSeconds { get; set; } = 3;

    [JsonProperty("join_timeout_minutes")]
    public int JoinTimeoutMinutes { get; set; } = 5;

    [JsonProperty("reconnect_grace_period_seconds")]
    public int ReconnectGracePeriodSeconds { get; set; } = 120;

    [JsonProperty("round_time_seconds")]
    public int RoundTimeSeconds { get; set; } = 115;

    [JsonProperty("freeze_time_seconds")]
    public int FreezeTimeSeconds { get; set; } = 15;

    [JsonProperty("team_swap_at_round")]
    public int TeamSwapAtRound { get; set; } = 9;

    [JsonProperty("max_rounds")]
    public int MaxRounds { get; set; } = 16;

    [JsonProperty("enable_demo_recording")]
    public bool EnableDemoRecording { get; set; } = true;

    [JsonProperty("demo_folder")]
    public string DemoFolder { get; set; } = "gotv";

    [JsonProperty("enable_skin_changer")]
    public bool EnableSkinChanger { get; set; } = true;

    [JsonProperty("skin_menu_command")]
    public string SkinMenuCommand { get; set; } = "!guns";

    [JsonProperty("default_map")]
    public string DefaultMap { get; set; } = "de_dust2";

    [JsonProperty("debug_mode")]
    public bool DebugMode { get; set; } = true;

    public static PluginConfig Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            var defaultConfig = new PluginConfig();
            File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
            return defaultConfig;
        }

        var json = File.ReadAllText(configPath);
        return JsonConvert.DeserializeObject<PluginConfig>(json) ?? new PluginConfig();
    }
}
