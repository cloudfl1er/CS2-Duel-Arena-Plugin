namespace CS2DuelServer;

public enum MatchState
{
    None,
    WaitingForPlayers,
    WaitingToStart,
    Warmup,
    Live,
    Finished
}

public class PlayerStats
{
    public string SteamId { get; set; } = "";
    public int Score { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Headshots { get; set; }
    public int Damage { get; set; }
    public bool IsConnected { get; set; }
    public DateTime? DisconnectTime { get; set; }
}

public class MatchManager
{
    private readonly PluginConfig _config;
    private readonly ApiClient _apiClient;

    public MatchData? CurrentMatch { get; private set; }
    public MatchState State { get; private set; } = MatchState.None;
    public PlayerStats Player1Stats { get; private set; } = new();
    public PlayerStats Player2Stats { get; private set; } = new();
    public int CurrentRound { get; private set; }
    public bool TeamsSwapped { get; private set; }
    public int SwapHappenedAtRound { get; private set; }
    public DateTime MatchStartTime { get; private set; }
    public string DemoFilePath { get; set; } = "";
    public string DisconnectType { get; set; } = "";

    public MatchManager(PluginConfig config, ApiClient apiClient)
    {
        _config = config;
        _apiClient = apiClient;
    }

    public void SetMatch(MatchData match)
    {
        CurrentMatch = match;
        State = MatchState.WaitingForPlayers;
        CurrentRound = 0;
        TeamsSwapped = false;
        SwapHappenedAtRound = 0;
        DemoFilePath = "";
        DisconnectType = "";

        Player1Stats = new PlayerStats { SteamId = match.Player1SteamId };
        Player2Stats = new PlayerStats { SteamId = match.Player2SteamId };
    }

    public void SetState(MatchState state)
    {
        State = state;
        if (state == MatchState.Live)
            MatchStartTime = DateTime.UtcNow;
    }

    public void OnRoundStart(int roundNumber)
    {
        CurrentRound = roundNumber;

        if (!TeamsSwapped && roundNumber >= _config.TeamSwapAtRound)
        {
            TeamsSwapped = true;
            SwapHappenedAtRound = roundNumber;
            _ = _apiClient.TrackTeamSwapAsync(CurrentMatch!.Id, roundNumber);
        }
    }

    public void RecordKill(string attackerSteamId, string victimSteamId, bool headshot)
    {
        if (attackerSteamId == Player1Stats.SteamId)
        {
            Player1Stats.Kills++;
            if (headshot) Player1Stats.Headshots++;
        }
        else if (attackerSteamId == Player2Stats.SteamId)
        {
            Player2Stats.Kills++;
            if (headshot) Player2Stats.Headshots++;
        }

        if (victimSteamId == Player1Stats.SteamId)
            Player1Stats.Deaths++;
        else if (victimSteamId == Player2Stats.SteamId)
            Player2Stats.Deaths++;
    }

    public void RecordRoundWin(string winnerSteamId)
    {
        if (winnerSteamId == Player1Stats.SteamId)
            Player1Stats.Score++;
        else if (winnerSteamId == Player2Stats.SteamId)
            Player2Stats.Score++;
    }

    public bool CheckMatchEnd(out string? winnerSteamId)
    {
        int winsNeeded = (_config.MaxRounds / 2) + 1;
        if (Player1Stats.Score >= winsNeeded)
        {
            winnerSteamId = Player1Stats.SteamId;
            return true;
        }
        if (Player2Stats.Score >= winsNeeded)
        {
            winnerSteamId = Player2Stats.SteamId;
            return true;
        }
        winnerSteamId = null;
        return false;
    }

    public PlayerStats GetPlayerStats(string steamId)
    {
        if (steamId == Player1Stats.SteamId) return Player1Stats;
        if (steamId == Player2Stats.SteamId) return Player2Stats;
        return new PlayerStats();
    }

    public bool BothPlayersConnected()
        => Player1Stats.IsConnected && Player2Stats.IsConnected;

    public bool IsPlayer1Winning()
        => Player1Stats.Score > Player2Stats.Score;

    public bool IsPlayer2Winning()
        => Player2Stats.Score > Player1Stats.Score;

    public MatchResult BuildResult(string winnerSteamId)
    {
        return new MatchResult
        {
            WinnerSteamId = winnerSteamId,
            Player1Score = Player1Stats.Score,
            Player2Score = Player2Stats.Score,
            Player1Kills = Player1Stats.Kills,
            Player2Kills = Player2Stats.Kills,
            Player1Deaths = Player1Stats.Deaths,
            Player2Deaths = Player2Stats.Deaths,
            Player1Headshots = Player1Stats.Headshots,
            Player2Headshots = Player2Stats.Headshots,
            Player1Damage = Player1Stats.Damage,
            Player2Damage = Player2Stats.Damage,
            MatchDurationSeconds = (int)(DateTime.UtcNow - MatchStartTime).TotalSeconds,
            TeamsSwapped = TeamsSwapped,
            SwapHappenedAtRound = SwapHappenedAtRound,
            DemoFilePath = DemoFilePath,
            DisconnectType = DisconnectType
        };
    }

    public void Reset()
    {
        CurrentMatch = null;
        State = MatchState.None;
        Player1Stats = new PlayerStats();
        Player2Stats = new PlayerStats();
        CurrentRound = 0;
        TeamsSwapped = false;
        SwapHappenedAtRound = 0;
        DemoFilePath = "";
        DisconnectType = "";
    }
}
