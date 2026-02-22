using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace CS2DuelServer;

[MinimumApiVersion(80)]
public class CS2DuelServer : BasePlugin
{
    public override string ModuleName => "CS2 Duel Arena Server";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "CS2 Duel Arena";
    public override string ModuleDescription => "Competitive 1v1 duel plugin for CS2";

    private PluginConfig _config = new();
    private ApiClient _apiClient = null!;
    private MatchManager _matchManager = null!;
    private PlayerManager _playerManager = null!;
    private DemoRecorder _demoRecorder = null!;
    private SkinChangerIntegration _skinChanger = null!;

    private CounterStrikeSharp.API.Modules.Timers.Timer? _pollTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _joinTimeoutTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _warmupTimer;
    private int _warmupCountdown;
    private bool _matchSetupInProgress;

    public override void Load(bool hotReload)
    {
        var configPath = Path.Combine(ModuleDirectory, "config.json");
        _config = PluginConfig.Load(configPath);

        _apiClient = new ApiClient(_config);
        _matchManager = new MatchManager(_config, _apiClient);
        _playerManager = new PlayerManager(_config, _matchManager);
        _demoRecorder = new DemoRecorder(_config);
        _skinChanger = new SkinChangerIntegration(_config, _apiClient);

        RegisterEventHandlers();
        RegisterCommands();
        StartPollingTimer();

        Console.WriteLine($"[CS2DuelServer] Plugin loaded. Server ID: {_config.ServerId}");
    }

    public override void Unload(bool hotReload)
    {
        _pollTimer?.Kill();
        _joinTimeoutTimer?.Kill();
        _warmupTimer?.Kill();
    }

    private void RegisterEventHandlers()
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
    }

    private void RegisterCommands()
    {
        AddCommand("css_duel_cancel", "Cancel the current duel", OnDuelCancel);
        AddCommand("css_duel_restart", "Restart the current duel", OnDuelRestart);
        AddCommand("css_duel_status", "Show duel status", OnDuelStatus);
        AddCommand("css_duel_force_end", "Force end the current duel", OnDuelForceEnd);

        var skinCmd = _config.SkinMenuCommand.TrimStart('!');
        AddCommand($"css_{skinCmd}", "Open skin menu", OnSkinMenuCommand);
    }

    private void StartPollingTimer()
    {
        _pollTimer = AddTimer(_config.PollIntervalSeconds, OnPollTimerTick, TimerFlags.REPEAT);
    }

    private void OnPollTimerTick()
    {
        Task.Run(async () =>
        {
            await _apiClient.SendHeartbeatAsync(GetCurrentStatus());

            if (_matchManager.State == MatchState.None && !_matchSetupInProgress)
            {
                var match = await _apiClient.GetCurrentMatchAsync();
                if (match != null)
                {
                    _matchSetupInProgress = true;
                    Server.NextFrame(() => SetupMatch(match));
                }
            }
        });
    }

    private string GetCurrentStatus()
    {
        return _matchManager.State switch
        {
            MatchState.None => "free",
            MatchState.WaitingForPlayers => "waiting_for_players",
            MatchState.WaitingToStart => "waiting_to_start",
            MatchState.Warmup => "waiting_to_start",
            MatchState.Live => "busy",
            MatchState.Finished => "busy",
            _ => "free"
        };
    }

    private void SetupMatch(MatchData match)
    {
        _matchManager.SetMatch(match);
        _playerManager.KickNonWhitelistedPlayers();

        Server.ExecuteCommand($"changelevel {match.Map}");
        Server.ExecuteCommand($"sv_password \"{match.ServerPassword}\"");
        Server.ExecuteCommand("mp_autoteambalance 0");
        Server.ExecuteCommand("bot_quota 0");
        Server.ExecuteCommand("bot_kick");
        var roundTimeMinutes = (_config.RoundTimeSeconds / 60.0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        Server.ExecuteCommand($"mp_roundtime {roundTimeMinutes}");
        Server.ExecuteCommand($"mp_freezetime {_config.FreezeTimeSeconds}");
        Server.ExecuteCommand("mp_c4timer 40");
        Server.ExecuteCommand("mp_friendlyfire 0");
        Server.ExecuteCommand("mp_solid_teammates 0");
        Server.ExecuteCommand($"mp_maxrounds {_config.MaxRounds}");
        Server.ExecuteCommand("mp_startmoney 16000");
        Server.ExecuteCommand("mp_warmup_pausetimer 0");
        Server.ExecuteCommand("tv_enable 1");

        Task.Run(async () =>
            await _apiClient.UpdateServerStatusAsync("waiting_for_players"));

        StartJoinTimeout();
        _matchSetupInProgress = false;

        if (_config.DebugMode)
            Console.WriteLine($"[CS2DuelServer] Match {match.Id} setup complete on {match.Map}");
    }

    private void StartJoinTimeout()
    {
        _joinTimeoutTimer?.Kill();
        _joinTimeoutTimer = AddTimer(
            _config.JoinTimeoutMinutes * 60f,
            OnJoinTimeout);
    }

    private void OnJoinTimeout()
    {
        if (_matchManager.State != MatchState.WaitingForPlayers) return;
        if (_matchManager.BothPlayersConnected()) return;

        var matchId = _matchManager.CurrentMatch?.Id ?? 0;
        Server.PrintToChatAll(" \x04[Duel Arena]\x01 Players did not join in time. Duel cancelled.");

        Task.Run(async () =>
        {
            if (matchId > 0)
                await _apiClient.CancelMatchAsync(matchId, "join_timeout");
        });

        ResetServer();
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        if (_matchManager.CurrentMatch == null) return HookResult.Continue;

        var steamId = player.SteamID.ToString();

        if (!_playerManager.IsAllowedToJoin(steamId))
        {
            player.PrintToChat(" \x04[Duel Arena]\x01 This server is reserved for a private duel.");
            AddTimer(0.5f, () =>
            {
                if (player.IsValid)
                    player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
            });
            return HookResult.Continue;
        }

        _playerManager.OnPlayerConnected(player);

        player.PrintToChat($" \x04[Duel Arena]\x01 Welcome to your duel! Match ID: \x05{_matchManager.CurrentMatch.Id}");

        Task.Run(async () => await _skinChanger.CheckAndEnableForPlayer(player));

        if (_matchManager.BothPlayersConnected() &&
            _matchManager.State == MatchState.WaitingForPlayers)
        {
            _matchManager.SetState(MatchState.WaitingToStart);
            _joinTimeoutTimer?.Kill();

            Task.Run(async () =>
                await _apiClient.UpdateServerStatusAsync("waiting_to_start"));

            StartWarmup();
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot) return HookResult.Continue;

        if (_matchManager.State != MatchState.Live) return HookResult.Continue;

        var steamId = player.SteamID.ToString();
        _playerManager.OnPlayerDisconnected(player);

        if (_playerManager.IsInEarlyRound())
        {
            var matchId = _matchManager.CurrentMatch?.Id ?? 0;
            Server.PrintToChatAll(" \x04[Duel Arena]\x01 Player disconnected in early rounds. Duel cancelled.");
            _matchManager.DisconnectType = "early_disconnect";

            Task.Run(async () =>
            {
                if (matchId > 0)
                    await _apiClient.CancelMatchAsync(matchId, "early_disconnect");
            });

            ResetServer();
            return HookResult.Continue;
        }

        var stats = _matchManager.GetPlayerStats(steamId);
        bool isWinning = steamId == _matchManager.CurrentMatch?.Player1SteamId
            ? _matchManager.IsPlayer1Winning()
            : _matchManager.IsPlayer2Winning();

        if (!isWinning)
        {
            _matchManager.DisconnectType = "disconnect_loss";
            DetermineWinnerByDisconnect(steamId);
        }
        else
        {
            _matchManager.DisconnectType = "disconnect_winning";
            Server.PrintToChatAll($" \x04[Duel Arena]\x01 Player disconnected while winning. {_config.ReconnectGracePeriodSeconds}s grace period.");
            StartDisconnectGracePeriod(steamId);
        }

        return HookResult.Continue;
    }

    private void StartDisconnectGracePeriod(string disconnectedSteamId)
    {
        AddTimer(_config.ReconnectGracePeriodSeconds, () =>
        {
            var stats = _matchManager.GetPlayerStats(disconnectedSteamId);
            if (stats.IsConnected) return;

            Server.PrintToChatAll(" \x04[Duel Arena]\x01 Grace period expired. Determining winner...");
            DetermineWinnerByDisconnect(disconnectedSteamId);
        });
    }

    private void DetermineWinnerByDisconnect(string disconnectedSteamId)
    {
        if (_matchManager.CurrentMatch == null) return;

        var winnerSteamId = disconnectedSteamId == _matchManager.CurrentMatch.Player1SteamId
            ? _matchManager.CurrentMatch.Player2SteamId
            : _matchManager.CurrentMatch.Player1SteamId;

        EndMatch(winnerSteamId);
    }

    private void StartWarmup()
    {
        _matchManager.SetState(MatchState.Warmup);
        _warmupCountdown = 15;

        Server.ExecuteCommand("mp_warmup_start");

        Server.PrintToChatAll($" \x04[Duel Arena]\x01 Match starting in \x05{_warmupCountdown}\x01 seconds!");
        ShowMatchInfo();

        _warmupTimer = AddTimer(1.0f, OnWarmupTick, TimerFlags.REPEAT);
    }

    private void OnWarmupTick()
    {
        _warmupCountdown--;

        if (_warmupCountdown > 0 && _warmupCountdown <= 5)
            Server.PrintToChatAll($" \x04[Duel Arena]\x01 Match starting in \x05{_warmupCountdown}\x01...");

        if (_warmupCountdown <= 0)
        {
            _warmupTimer?.Kill();
            _warmupTimer = null;
            StartMatch();
        }
    }

    private void ShowMatchInfo()
    {
        if (_matchManager.CurrentMatch == null) return;

        Server.PrintToChatAll($" \x04[Duel Arena]\x01 ═══════════════════════════");
        Server.PrintToChatAll($" \x04[Duel Arena]\x01 Match \x05#{_matchManager.CurrentMatch.Id}");
        Server.PrintToChatAll($" \x04[Duel Arena]\x01 Map: \x05{_matchManager.CurrentMatch.Map}");
        Server.PrintToChatAll($" \x04[Duel Arena]\x01 Format: MR{_config.MaxRounds / 2} Competitive");
        Server.PrintToChatAll($" \x04[Duel Arena]\x01 ═══════════════════════════");
    }

    private void StartMatch()
    {
        Server.ExecuteCommand("mp_warmup_end");
        Server.ExecuteCommand("mp_restartgame 1");

        _matchManager.SetState(MatchState.Live);

        if (_config.EnableDemoRecording && _matchManager.CurrentMatch != null)
            _matchManager.DemoFilePath = _demoRecorder.StartRecording(_matchManager.CurrentMatch.Id);

        Task.Run(async () =>
            await _apiClient.UpdateServerStatusAsync("busy"));

        Server.PrintToChatAll(" \x04[Duel Arena]\x01 \x05MATCH IS LIVE!\x01 Good luck!");
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (_matchManager.State != MatchState.Live) return HookResult.Continue;

        _matchManager.OnRoundStart(_matchManager.CurrentRound + 1);

        if (_matchManager.TeamsSwapped && _matchManager.SwapHappenedAtRound == _matchManager.CurrentRound)
            Server.PrintToChatAll(" \x04[Duel Arena]\x01 Teams have swapped sides!");

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (_matchManager.State != MatchState.Live) return HookResult.Continue;

        var winner = (CsTeam)@event.Winner;
        string? winnerSteamId = null;

        if (_matchManager.CurrentMatch != null)
        {
            bool p1IsTerrorist = !_matchManager.TeamsSwapped;

            if (winner == CsTeam.Terrorist)
                winnerSteamId = p1IsTerrorist
                    ? _matchManager.CurrentMatch.Player1SteamId
                    : _matchManager.CurrentMatch.Player2SteamId;
            else if (winner == CsTeam.CounterTerrorist)
                winnerSteamId = p1IsTerrorist
                    ? _matchManager.CurrentMatch.Player2SteamId
                    : _matchManager.CurrentMatch.Player1SteamId;

            if (winnerSteamId != null)
            {
                _matchManager.RecordRoundWin(winnerSteamId);

                Server.PrintToChatAll(
                    $" \x04[Duel Arena]\x01 Score: \x05{_matchManager.Player1Stats.Score}\x01 - \x05{_matchManager.Player2Stats.Score}");

                if (_matchManager.CheckMatchEnd(out var matchWinner) && matchWinner != null)
                {
                    AddTimer(2.0f, () => EndMatch(matchWinner));
                }
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (_matchManager.State != MatchState.Live) return HookResult.Continue;

        var attacker = @event.Attacker;
        var victim = @event.Userid;

        if (attacker == null || victim == null) return HookResult.Continue;

        var attackerSteamId = attacker.SteamID.ToString();
        var victimSteamId = victim.SteamID.ToString();

        _matchManager.RecordKill(attackerSteamId, victimSteamId, @event.Headshot);

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (_matchManager.State != MatchState.Live) return HookResult.Continue;

        var attacker = @event.Attacker;
        if (attacker == null) return HookResult.Continue;

        var attackerSteamId = attacker.SteamID.ToString();

        if (attackerSteamId == _matchManager.Player1Stats.SteamId)
            _matchManager.Player1Stats.Damage += @event.DmgHealth;
        else if (attackerSteamId == _matchManager.Player2Stats.SteamId)
            _matchManager.Player2Stats.Damage += @event.DmgHealth;

        return HookResult.Continue;
    }

    private void EndMatch(string winnerSteamId)
    {
        if (_matchManager.State == MatchState.Finished) return;

        _matchManager.SetState(MatchState.Finished);

        if (_demoRecorder.IsRecording)
            _matchManager.DemoFilePath = _demoRecorder.StopRecording();

        Server.ExecuteCommand("mp_restartgame 3");
        Server.PrintToChatAll($" \x04[Duel Arena]\x01 \x05MATCH OVER!\x01 Winner: \x05{winnerSteamId}");
        Server.PrintToChatAll(
            $" \x04[Duel Arena]\x01 Final Score: \x05{_matchManager.Player1Stats.Score}\x01 - \x05{_matchManager.Player2Stats.Score}");

        var result = _matchManager.BuildResult(winnerSteamId);
        var matchId = _matchManager.CurrentMatch?.Id ?? 0;

        Task.Run(async () =>
        {
            if (matchId > 0)
                await _apiClient.ReportMatchResultAsync(matchId, result);
        });

        AddTimer(10.0f, ResetServer);
    }

    private void ResetServer()
    {
        _demoRecorder.StopRecording();
        _skinChanger.ClearAuthorizations();
        _playerManager.KickAllPlayers();

        _matchManager.Reset();
        _matchSetupInProgress = false;

        _joinTimeoutTimer?.Kill();
        _warmupTimer?.Kill();

        Server.ExecuteCommand("sv_password \"\"");
        Server.ExecuteCommand($"changelevel {_config.DefaultMap}");

        Task.Run(async () =>
            await _apiClient.UpdateServerStatusAsync("free"));

        Console.WriteLine("[CS2DuelServer] Server reset to free state.");
    }

    // Admin Commands
    private void OnDuelCancel(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (_matchManager.CurrentMatch == null)
        {
            commandInfo.ReplyToCommand("[CS2DuelServer] No active match to cancel.");
            return;
        }

        var matchId = _matchManager.CurrentMatch.Id;
        Task.Run(async () =>
            await _apiClient.CancelMatchAsync(matchId, "admin_cancel"));

        ResetServer();
        commandInfo.ReplyToCommand("[CS2DuelServer] Match cancelled.");
    }

    private void OnDuelRestart(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (_matchManager.CurrentMatch == null)
        {
            commandInfo.ReplyToCommand("[CS2DuelServer] No active match to restart.");
            return;
        }

        Server.ExecuteCommand("mp_restartgame 3");
        commandInfo.ReplyToCommand("[CS2DuelServer] Match restarted.");
    }

    private void OnDuelStatus(CCSPlayerController? player, CommandInfo commandInfo)
    {
        commandInfo.ReplyToCommand($"[CS2DuelServer] State: {_matchManager.State}");
        if (_matchManager.CurrentMatch != null)
        {
            commandInfo.ReplyToCommand($"[CS2DuelServer] Match ID: {_matchManager.CurrentMatch.Id}");
            commandInfo.ReplyToCommand($"[CS2DuelServer] Map: {_matchManager.CurrentMatch.Map}");
            commandInfo.ReplyToCommand($"[CS2DuelServer] Score: {_matchManager.Player1Stats.Score} - {_matchManager.Player2Stats.Score}");
            commandInfo.ReplyToCommand($"[CS2DuelServer] Round: {_matchManager.CurrentRound}");
        }
    }

    private void OnDuelForceEnd(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (_matchManager.CurrentMatch == null)
        {
            commandInfo.ReplyToCommand("[CS2DuelServer] No active match.");
            return;
        }

        var winnerSteamId = _matchManager.Player1Stats.Score >= _matchManager.Player2Stats.Score
            ? _matchManager.Player1Stats.SteamId
            : _matchManager.Player2Stats.SteamId;

        EndMatch(winnerSteamId);
        commandInfo.ReplyToCommand("[CS2DuelServer] Match force-ended.");
    }

    private void OnSkinMenuCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid) return;
        _skinChanger.ShowSkinMenu(player);
    }
}
