using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CS2DuelServer;

public class SkinChangerIntegration
{
    private readonly PluginConfig _config;
    private readonly ApiClient _apiClient;
    private readonly HashSet<string> _authorizedSteamIds = new();

    public SkinChangerIntegration(PluginConfig config, ApiClient apiClient)
    {
        _config = config;
        _apiClient = apiClient;
    }

    public async Task CheckAndEnableForPlayer(CCSPlayerController player)
    {
        if (!_config.EnableSkinChanger) return;

        var steamId = player.SteamID.ToString();
        var subscription = await _apiClient.GetUserSubscriptionAsync(steamId);

        if (subscription?.Active == true)
        {
            _authorizedSteamIds.Add(steamId);
            player.PrintToChat($" \x04[Duel Arena]\x01 Skin changer enabled! Use \x05{_config.SkinMenuCommand}\x01 to select skins.");
            if (_config.DebugMode)
                Console.WriteLine($"[CS2DuelServer] Skin changer enabled for {steamId} (plan: {subscription.Plan})");
        }
    }

    public bool IsAuthorized(string steamId)
        => _authorizedSteamIds.Contains(steamId);

    public void ShowSkinMenu(CCSPlayerController player)
    {
        if (!_config.EnableSkinChanger) return;

        var steamId = player.SteamID.ToString();
        if (!IsAuthorized(steamId))
        {
            player.PrintToChat(" \x04[Duel Arena]\x01 Skin changer requires an active subscription.");
            return;
        }

        player.PrintToChat(" \x04[Duel Arena]\x01 Opening skin menu...");
        Server.ExecuteCommand($"css_guns {steamId}");
    }

    public void ClearAuthorizations()
        => _authorizedSteamIds.Clear();

    public void RemoveAuthorization(string steamId)
        => _authorizedSteamIds.Remove(steamId);
}
