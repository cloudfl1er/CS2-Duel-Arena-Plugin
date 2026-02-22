using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace CS2DuelServer;

public class PlayerManager
{
    private readonly PluginConfig _config;
    private readonly MatchManager _matchManager;

    public PlayerManager(PluginConfig config, MatchManager matchManager)
    {
        _config = config;
        _matchManager = matchManager;
    }

    public bool IsAllowedToJoin(string steamId)
    {
        if (_matchManager.CurrentMatch == null)
            return false;

        return steamId == _matchManager.CurrentMatch.Player1SteamId
            || steamId == _matchManager.CurrentMatch.Player2SteamId;
    }

    public void KickNonWhitelistedPlayers()
    {
        if (_matchManager.CurrentMatch == null) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot) continue;

            var steamId = player.SteamID.ToString();
            if (!IsAllowedToJoin(steamId))
            {
                player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                if (_config.DebugMode)
                    Console.WriteLine($"[CS2DuelServer] Kicked non-whitelisted player: {steamId}");
            }
        }
    }

    public void KickAllPlayers()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot) continue;
            player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
        }
    }

    public CCSPlayerController? FindPlayerBySteamId(string steamId)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot) continue;
            if (player.SteamID.ToString() == steamId)
                return player;
        }
        return null;
    }

    public void OnPlayerConnected(CCSPlayerController player)
    {
        if (_matchManager.CurrentMatch == null) return;

        var steamId = player.SteamID.ToString();
        var stats = _matchManager.GetPlayerStats(steamId);
        stats.IsConnected = true;
        stats.DisconnectTime = null;
    }

    public void OnPlayerDisconnected(CCSPlayerController player)
    {
        if (_matchManager.CurrentMatch == null) return;

        var steamId = player.SteamID.ToString();
        var stats = _matchManager.GetPlayerStats(steamId);
        stats.IsConnected = false;
        stats.DisconnectTime = DateTime.UtcNow;
    }

    public bool IsInEarlyRound()
        => _matchManager.CurrentRound <= 3;
}
