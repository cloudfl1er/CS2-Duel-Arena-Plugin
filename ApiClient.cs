using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CS2DuelServer;

public class MatchData
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("map")]
    public string Map { get; set; } = "de_dust2";

    [JsonProperty("server_password")]
    public string ServerPassword { get; set; } = "";

    [JsonProperty("player1_steam_id")]
    public string Player1SteamId { get; set; } = "";

    [JsonProperty("player2_steam_id")]
    public string Player2SteamId { get; set; } = "";
}

public class SubscriptionData
{
    [JsonProperty("active")]
    public bool Active { get; set; }

    [JsonProperty("plan")]
    public string Plan { get; set; } = "";
}

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly PluginConfig _config;

    public ApiClient(PluginConfig config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("api-key", config.ApiKey);
    }

    public async Task<bool> SendHeartbeatAsync(string status)
    {
        try
        {
            var payload = new { status };
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/api/server/{_config.ServerId}/heartbeat", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CS2DuelServer] Heartbeat error: {ex.Message}");
            return false;
        }
    }

    public async Task<MatchData?> GetCurrentMatchAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/server/{_config.ServerId}/current_match");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<MatchData>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CS2DuelServer] GetCurrentMatch error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateServerStatusAsync(string status)
    {
        try
        {
            var payload = new { status };
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PatchAsync(
                $"/api/server/{_config.ServerId}/status", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CS2DuelServer] UpdateServerStatus error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ReportMatchResultAsync(int matchId, MatchResult result)
    {
        try
        {
            var content = new StringContent(
                JsonConvert.SerializeObject(result),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/api/matches/{matchId}/result", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CS2DuelServer] ReportMatchResult error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TrackTeamSwapAsync(int matchId, int swapRound)
    {
        try
        {
            var payload = new { swap_happened_at_round = swapRound };
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/api/matches/{matchId}/team_swap", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CS2DuelServer] TrackTeamSwap error: {ex.Message}");
            return false;
        }
    }

    public async Task<SubscriptionData?> GetUserSubscriptionAsync(string steamId)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/users/{steamId}/subscription");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<SubscriptionData>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CS2DuelServer] GetUserSubscription error: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CancelMatchAsync(int matchId, string reason)
    {
        try
        {
            var payload = new { reason };
            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/api/matches/{matchId}/cancel", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CS2DuelServer] CancelMatch error: {ex.Message}");
            return false;
        }
    }
}

public class MatchResult
{
    [JsonProperty("winner_steam_id")]
    public string WinnerSteamId { get; set; } = "";

    [JsonProperty("player1_score")]
    public int Player1Score { get; set; }

    [JsonProperty("player2_score")]
    public int Player2Score { get; set; }

    [JsonProperty("player1_kills")]
    public int Player1Kills { get; set; }

    [JsonProperty("player2_kills")]
    public int Player2Kills { get; set; }

    [JsonProperty("player1_deaths")]
    public int Player1Deaths { get; set; }

    [JsonProperty("player2_deaths")]
    public int Player2Deaths { get; set; }

    [JsonProperty("player1_headshots")]
    public int Player1Headshots { get; set; }

    [JsonProperty("player2_headshots")]
    public int Player2Headshots { get; set; }

    [JsonProperty("player1_damage")]
    public int Player1Damage { get; set; }

    [JsonProperty("player2_damage")]
    public int Player2Damage { get; set; }

    [JsonProperty("match_duration_seconds")]
    public int MatchDurationSeconds { get; set; }

    [JsonProperty("teams_swapped")]
    public bool TeamsSwapped { get; set; }

    [JsonProperty("swap_happened_at_round")]
    public int SwapHappenedAtRound { get; set; }

    [JsonProperty("demo_file_path")]
    public string DemoFilePath { get; set; } = "";

    [JsonProperty("disconnect_type")]
    public string DisconnectType { get; set; } = "";
}
