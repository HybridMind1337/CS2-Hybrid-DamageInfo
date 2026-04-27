using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace HybridDamageInfo;

[MinimumApiVersion(200)]
public class HybridDamageInfoPlugin : BasePlugin, IPluginConfig<HybridDamageInfoConfig>
{
    public override string ModuleName    => "Hybrid-DamageInfo";
    public override string ModuleVersion => "5.0.0";
    public override string ModuleAuthor  => "HybridMind";
    public override string ModuleDescription => "Clean and compact damage info for CS2.";

    public HybridDamageInfoConfig Config { get; set; } = new();

    private readonly DamageTracker           _tracker = new();
    private readonly PlayerPreferenceManager _prefs   = new();
    private Dictionary<string, string>       _lang    = new();
    private CCSGameRules?                    _gameRules;

    private string Prefix => $" {ChatColors.Red}[DI]{ChatColors.Default}";

    public void OnConfigParsed(HybridDamageInfoConfig config)
    {
        Config = config;
        LoadLanguage(config.Language);
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnTick>(OnTick);

        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        AddCommand("css_di",         "Hybrid-DamageInfo menu/toggle", OnDiCommand);
        AddCommand("css_damageinfo", "Hybrid-DamageInfo menu/toggle", OnDiCommand);

        OnMapStart(string.Empty);
    }

    private void OnMapStart(string mapName)
    {
        AddTimer(1.0f, () =>
        {
            _gameRules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                .FirstOrDefault()?.GameRules;
        });
    }

    private bool IsWarmup() => _gameRules?.WarmupPeriod ?? false;

    private void OnTick()
    {
        if (IsWarmup()) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot || player.IsHLTV) continue;

            var data = _tracker.Get(player.Slot);
            if (data != null && !string.IsNullOrEmpty(data.CenterMessage))
                player.PrintToCenterHtml(data.CenterMessage);
        }
    }

    private void LoadLanguage(string lang)
    {
        string path = Path.Combine(ModuleDirectory, "lang", $"{lang}.json");
        if (!File.Exists(path))
            path = Path.Combine(ModuleDirectory, "lang", "en.json");
        if (!File.Exists(path)) return;

        try { _lang = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new(); }
        catch (Exception ex) { Console.WriteLine($"[DI] Lang error: {ex.Message}"); }
    }

    private string T(string key) =>
        _lang.TryGetValue(key, out var v) ? v : key;

    private void OnDiCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return;

        ulong steamId = player.SteamID;
        string arg    = info.ArgCount > 1 ? info.ArgByIndex(1).Trim() : "";

        if (string.IsNullOrEmpty(arg))
        {
            player.PrintToCenterHtml(_prefs.BuildMenuHtml(steamId), 8);
            player.PrintToChat($"{Prefix} {ChatColors.Yellow}{T("toggle.usage")}");
            return;
        }

        if (int.TryParse(arg, out int num))
        {
            var (setting, newState) = _prefs.ToggleByNumber(steamId, num);
            if (string.IsNullOrEmpty(setting))
            {
                player.PrintToChat($"{Prefix} {ChatColors.Yellow}{T("toggle.usage")}");
                return;
            }
            player.PrintToChat($"{Prefix} {ChatColors.Green}{T($"toggle.{setting}.{(newState ? "on" : "off")}")}");
            player.PrintToCenterHtml(_prefs.BuildMenuHtml(steamId), 8);
            return;
        }

        if (arg == "chat" || arg == "hud" || arg == "summary")
        {
            bool newState = _prefs.Toggle(steamId, arg);
            player.PrintToChat($"{Prefix} {ChatColors.Green}{T($"toggle.{arg}.{(newState ? "on" : "off")}")}");
            player.PrintToCenterHtml(_prefs.BuildMenuHtml(steamId), 8);
            return;
        }

        player.PrintToChat($"{Prefix} {ChatColors.Yellow}{T("toggle.usage")}");
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid) return HookResult.Continue;

        var data = _tracker.GetOrCreate(player.Slot);
        data.IsDataShown = false;
        _tracker.CacheName(player.Slot, player.PlayerName);

        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        if (IsWarmup()) return HookResult.Continue;

        var attacker = @event.Attacker;
        var victim   = @event.Userid;

        if (attacker == null || !attacker.IsValid || victim == null || !victim.IsValid)
            return HookResult.Continue;

        if (attacker.Slot == victim.Slot) return HookResult.Continue;

        if (!Config.ShowBotDamage && (attacker.IsBot || victim.IsBot))
            return HookResult.Continue;

        bool friendlyFire = attacker.TeamNum == victim.TeamNum;
        if (friendlyFire && !Config.ShowFriendlyFire) return HookResult.Continue;

        _tracker.CacheName(attacker.Slot, attacker.PlayerName);
        _tracker.CacheName(victim.Slot,   victim.PlayerName);

        _tracker.RecordDamage(attacker.Slot, victim.Slot, @event.DmgHealth, @event.DmgArmor, @event.Hitgroup, friendlyFire);

        if (!attacker.IsBot && Config.ShowCenterHUD && _prefs.Get(attacker.SteamID).HudEnabled)
            ShowLiveHud(attacker, victim.PlayerName, attacker.Slot, victim.Slot, friendlyFire);

        return HookResult.Continue;
    }

    private void ShowLiveHud(CCSPlayerController attacker, string victimName, int attackerSlot, int victimSlot, bool ff)
    {
        var data   = _tracker.GetOrCreate(attackerSlot);
        var recent = data.RecentDamages.TryGetValue(victimSlot, out var r) ? r : null;
        if (recent == null) return;

        string ffTag   = ff ? " <font color='#ffcc00'>[FF]</font>" : "";
        string hgTag   = !string.IsNullOrEmpty(recent.LastHitgroup) ? $"  <font color='#aaaaaa'>[{recent.LastHitgroup}]</font>" : "";

        data.CenterTimer?.Kill();
        data.CenterTimer = null;

        data.CenterMessage =
            $"<font color='#ffffff'><b>{victimName}</b></font>{ffTag}<br>" +
            $"<font color='#ff4444'>-{recent.TotalDamage} HP</font>{hgTag}";

        data.CenterTimer = AddTimer(Config.HUDDisplaySeconds, () =>
        {
            data.CenterMessage = null;
            data.CenterTimer   = null;
        });
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        if (IsWarmup()) return HookResult.Continue;

        var victim = @event.Userid;
        if (victim == null || !victim.IsValid) return HookResult.Continue;

        var attacker = @event.Attacker;
        var data     = _tracker.GetOrCreate(victim.Slot);
        data.VictimKillerSlot = attacker != null && attacker.IsValid ? attacker.Slot : -1;

        if (!victim.IsBot)
            DisplayDeathInfo(victim);

        return HookResult.Continue;
    }

    private void DisplayDeathInfo(CCSPlayerController victim)
    {
        var data = _tracker.Get(victim.Slot);
        if (data == null || data.IsDataShown) return;

        data.IsDataShown = true;
        var pref = _prefs.Get(victim.SteamID);

        var takenDamage = data.TakenDamage
            .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
            .OrderByDescending(e => e.Value.DamageHP)
            .ToList();

        var givenDamage = data.GivenDamage
            .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
            .OrderByDescending(e => e.Value.DamageHP)
            .ToList();

        // ── Chat — 1 компактен ред ────────────────────────────────────────
        if (Config.ShowChatMessage && pref.ChatEnabled)
        {
            var killer = takenDamage.FirstOrDefault();
            if (killer.Value != null)
            {
                string name  = _tracker.GetName(killer.Key);
                string hs    = killer.Value.Headshot ? $" {ChatColors.Yellow}[HS]{ChatColors.Default}" : "";
                string ff    = killer.Value.IsFriendlyFire ? $" {ChatColors.Yellow}[FF]{ChatColors.Default}" : "";
                victim.PrintToChat(
                    $"{Prefix}{ff} ☠ {ChatColors.LightRed}{name}{ChatColors.Default} " +
                    $"{ChatColors.Yellow}{killer.Value.DamageHP} HP{ChatColors.Default} · " +
                    $"{killer.Value.Hits} hit(s){hs}");
            }

            if (givenDamage.Count > 0)
            {
                int totalHp = givenDamage.Sum(e => e.Value.DamageHP);
                string targets = string.Join(" · ", givenDamage.Select(e =>
                    $"{ChatColors.Green}{_tracker.GetName(e.Key)}{ChatColors.Default} " +
                    $"{ChatColors.Yellow}{e.Value.DamageHP}{ChatColors.Default}HP"));
                victim.PrintToChat($"{Prefix} ▸ {targets}");
            }
        }

        // ── Death HUD ─────────────────────────────────────────────────────
        if (Config.ShowCenterHUD && pref.HudEnabled)
        {
            var lines = new List<string>();

            if (takenDamage.Count > 0)
            {
                var killer = takenDamage.First();
                string name = _tracker.GetName(killer.Key);
                string hs   = killer.Value.Headshot ? " <font color='#ffdd00'>★ HS</font>" : "";
                string ff   = killer.Value.IsFriendlyFire ? " <font color='#ffcc00'>[FF]</font>" : "";
                lines.Add($"<font color='#ff4444'>☠</font> <b>{name}</b>{ff} — {killer.Value.DamageHP} HP · {killer.Value.Hits} hit(s){hs}");
            }

            if (givenDamage.Count > 0)
            {
                string targets = string.Join(" · ", givenDamage.Take(4).Select(e =>
                {
                    int remaining = GetRemainingHp(e.Key);
                    string name   = _tracker.GetName(e.Key);
                    return $"<b>{name}</b> {e.Value.DamageHP}→{remaining}HP";
                }));
                lines.Add($"<font color='#44aaff'>▸</font> {targets}");
            }

            if (lines.Count > 0)
            {
                data.CenterTimer?.Kill();
                data.CenterTimer   = null;
                data.CenterMessage = string.Join("<br>", lines);

                data.CenterTimer = AddTimer(Config.HUDDisplaySeconds, () =>
                {
                    data.CenterMessage = null;
                    data.CenterTimer   = null;
                });
            }
        }
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!Config.ShowRoundEndSummary || IsWarmup())
        {
            _tracker.ClearRound();
            return HookResult.Continue;
        }

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV)
            .ToList();

        foreach (var player in players)
            DisplayRoundSummary(player);

        var leaderboard = new List<(string Name, int TotalDmg)>();
        foreach (var player in players)
        {
            var data = _tracker.Get(player.Slot);
            if (data == null) continue;
            int total = data.GivenDamage
                .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
                .Sum(e => e.Value.DamageHP);
            if (total > 0)
                leaderboard.Add((player.PlayerName, total));
        }

        if (leaderboard.Count > 0)
        {
            var ranked = leaderboard.OrderByDescending(t => t.TotalDmg).ToList();
            string board = string.Join("  ", ranked.Take(5).Select((t, i) =>
            {
                string medal = i == 0 ? "🥇" : i == 1 ? "🥈" : i == 2 ? "🥉" : $"#{i + 1}";
                return $"{medal}{ChatColors.Green}{t.Name}{ChatColors.Default} {ChatColors.Yellow}{t.TotalDmg}{ChatColors.Default}HP";
            }));

            foreach (var player in players)
            {
                if (!_prefs.Get(player.SteamID).SummaryEnabled) continue;
                player.PrintToChat($"{Prefix} {board}");
            }
        }

        _tracker.ClearRound();
        return HookResult.Continue;
    }

    private void DisplayRoundSummary(CCSPlayerController player)
    {
        var data = _tracker.Get(player.Slot);
        if (data == null || data.IsDataShown) return;

        var pref = _prefs.Get(player.SteamID);
        if (!pref.SummaryEnabled) return;

        var given = data.GivenDamage
            .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
            .OrderByDescending(e => e.Value.DamageHP)
            .ToList();

        if (given.Count == 0) return;

        data.IsDataShown = true;

        string summary = string.Join(" · ", given.Select(e =>
        {
            string name = _tracker.GetName(e.Key);
            string hs   = e.Value.Headshot ? "★" : "";
            return $"{ChatColors.Green}{name}{ChatColors.Default} {ChatColors.Yellow}{e.Value.DamageHP}{ChatColors.Default}HP{hs} x{e.Value.Hits}";
        }));

        player.PrintToChat($"{Prefix} ▸ {summary}");
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _tracker.ClearRound();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
            _prefs.Remove(player.SteamID);
        return HookResult.Continue;
    }

    private int GetRemainingHp(int slot)
    {
        var target = Utilities.GetPlayerFromSlot(slot);
        return target?.PlayerPawn?.Value?.Health ?? 0;
    }
}
