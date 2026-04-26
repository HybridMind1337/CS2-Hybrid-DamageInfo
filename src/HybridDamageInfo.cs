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
    public override string ModuleVersion => "4.0.0";
    public override string ModuleAuthor  => "HybridMind";
    public override string ModuleDescription => "Displays damage info after death and round end.";

    public HybridDamageInfoConfig Config { get; set; } = new();

    private readonly DamageTracker           _tracker = new();
    private readonly PlayerPreferenceManager _prefs   = new();
    private Dictionary<string, string>       _lang    = new();
    private CCSGameRules?                    _gameRules;

    private string Prefix => $" {ChatColors.Red}[Hybrid-DamageInfo]{ChatColors.Default}";

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

    private void OnTick()
    {
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV
                
                && p.Team > CsTeam.Spectator);

        foreach (var player in players)
        {
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
        catch (Exception ex) { Console.WriteLine($"[Hybrid-DamageInfo] Lang error: {ex.Message}"); }
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
        var attacker = @event.Attacker;
        var victim   = @event.Userid;

        if (attacker == null || !attacker.IsValid || victim == null || !victim.IsValid)
            return HookResult.Continue;

        if (attacker.Slot == victim.Slot)
            return HookResult.Continue;

        if (!Config.ShowBotDamage && (attacker.IsBot || victim.IsBot))
            return HookResult.Continue;

        bool friendlyFire = attacker.TeamNum == victim.TeamNum;
        if (friendlyFire && !Config.ShowFriendlyFire)
            return HookResult.Continue;

        if (_gameRules != null && _gameRules.WarmupPeriod)
            return HookResult.Continue;

        _tracker.CacheName(attacker.Slot, attacker.PlayerName);
        _tracker.CacheName(victim.Slot,   victim.PlayerName);

        string hitgroup = DamageTracker.HitgroupToString(@event.Hitgroup);

        _tracker.RecordDamage(attacker.Slot, victim.Slot, @event.DmgHealth, @event.DmgArmor, hitgroup, friendlyFire);

        if (!attacker.IsBot && Config.ShowCenterHUD)
        {
            var attackerPref = _prefs.Get(attacker.SteamID);
            if (attackerPref.HudEnabled)
                ShowLiveCenterHud(attacker, victim, @event.DmgHealth, @event.DmgArmor, hitgroup);
        }

        if (Config.ShowConsoleLog && !attacker.IsBot)
        {
            string ffTag = friendlyFire ? " [FF]" : "";
            attacker.PrintToConsole($"[Hybrid-DamageInfo]{ffTag} {victim.PlayerName} — {@event.DmgHealth} HP / {@event.DmgArmor} Armor [{hitgroup}]");
        }

        return HookResult.Continue;
    }

    private void ShowLiveCenterHud(CCSPlayerController attacker, CCSPlayerController victim, int dmgHp, int dmgArmor, string hitgroup)
    {
        var data       = _tracker.GetOrCreate(attacker.Slot);
        var recent     = data.RecentDamages.TryGetValue(victim.Slot, out var r) ? r : null;
        int totalDmg   = recent?.TotalDamage ?? dmgHp;
        string ffTag   = attacker.TeamNum == victim.TeamNum ? " <font color='#ffcc00'>[FF]</font>" : "";

        data.CenterTimer?.Kill();
        data.CenterTimer = null;

        data.CenterMessage =
            $"<b>{victim.PlayerName}</b>{ffTag}<br>" +
            $"<font color='#ff4444'>{totalDmg} HP</font> / " +
            $"<font color='#4488ff'>{dmgArmor} Armor</font><br>" +
            $"<font color='#aaaaaa'>[{hitgroup}]</font>";

        data.CenterTimer = AddTimer(Config.HUDDisplaySeconds, () =>
        {
            data.CenterMessage = null;
            data.CenterTimer   = null;
        });
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        if (victim == null || !victim.IsValid) return HookResult.Continue;

        if (_gameRules != null && _gameRules.WarmupPeriod) return HookResult.Continue;

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
        var pref         = _prefs.Get(victim.SteamID);

        var takenDamage = data.TakenDamage
            .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
            .OrderByDescending(e => e.Value.DamageHP)
            .ToList();

        var givenDamage = data.GivenDamage
            .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
            .OrderByDescending(e => e.Value.DamageHP)
            .ToList();

        if (Config.ShowChatMessage && pref.ChatEnabled)
        {
            if (Config.CompactDeathMessage)
            {
                var killer = takenDamage.FirstOrDefault();
                if (killer.Value != null)
                {
                    string name   = _tracker.GetName(killer.Key);
                    string ffTag  = killer.Value.IsFriendlyFire ? $" {ChatColors.Yellow}[FF]{ChatColors.Default}" : "";
                    string hitStr = ChatHitgroupSummary(killer.Value);
                    victim.PrintToChat(
                        $"{Prefix}{ffTag} {ChatColors.LightRed}{name}{ChatColors.Default} dealt " +
                        $"{ChatColors.Yellow}{killer.Value.DamageHP} HP{ChatColors.Default} / " +
                        $"{ChatColors.LightBlue}{killer.Value.DamageArmor} Armor{ChatColors.Default} " +
                        $"({killer.Value.Hits} hit(s)){hitStr}");
                }

                if (givenDamage.Count > 0)
                {
                    int totalHp = givenDamage.Sum(e => e.Value.DamageHP);
                    victim.PrintToChat(
                        $"{Prefix} You dealt {ChatColors.Yellow}{totalHp} HP{ChatColors.Default} " +
                        $"across {ChatColors.Green}{givenDamage.Count} player(s){ChatColors.Default}" +
                        $" — details in round summary");
                }
            }
            else
            {
                foreach (var (slot, entry) in takenDamage)
                {
                    string name   = _tracker.GetName(slot);
                    string ffTag  = entry.IsFriendlyFire ? $" {ChatColors.Yellow}[FF]{ChatColors.Default}" : "";
                    string hitStr = ChatHitgroupSummary(entry);
                    victim.PrintToChat(
                        $"{Prefix}{ffTag} {ChatColors.LightRed}{name}{ChatColors.Default} dealt " +
                        $"{ChatColors.Yellow}{entry.DamageHP} HP{ChatColors.Default} / " +
                        $"{ChatColors.LightBlue}{entry.DamageArmor} Armor{ChatColors.Default} " +
                        $"({entry.Hits} hit(s)){hitStr}");
                }

                foreach (var (slot, entry) in givenDamage)
                {
                    string name      = _tracker.GetName(slot);
                    int    remaining = GetRemainingHp(slot);
                    string hitStr    = ChatHitgroupSummary(entry);
                    victim.PrintToChat(
                        $"{Prefix} You dealt {ChatColors.Yellow}{entry.DamageHP} HP{ChatColors.Default} / " +
                        $"{ChatColors.LightBlue}{entry.DamageArmor} Armor{ChatColors.Default} to " +
                        $"{ChatColors.Green}{name}{ChatColors.Default} " +
                        $"({entry.Hits} hit(s)){hitStr} {ChatColors.Default}— " +
                        $"{ChatColors.Red}{remaining} HP left{ChatColors.Default}");
                }
            }
        }

        if (Config.ShowCenterHUD && pref.HudEnabled)
        {
            var lines = new List<string>();

            if (takenDamage.Count > 0)
            {
                lines.Add($"<b>{T("received.header")}</b>");
                foreach (var (slot, entry) in takenDamage)
                {
                    string name   = _tracker.GetName(slot);
                    string ffTag  = entry.IsFriendlyFire ? " <font color='#ffcc00'>[FF]</font>" : "";
                    string hitStr = HudHitgroupSummary(entry);
                    lines.Add($"{name}{ffTag}: <b>{entry.DamageHP} HP</b> / {entry.DamageArmor} Armor ({entry.Hits} hits){hitStr}");
                }
            }

            if (givenDamage.Count > 0)
            {
                lines.Add($"<b>{T("dealt.header")}</b>");
                foreach (var (slot, entry) in givenDamage)
                {
                    string name      = _tracker.GetName(slot);
                    int    remaining = GetRemainingHp(slot);
                    string hitStr    = HudHitgroupSummary(entry);
                    lines.Add($"{name}: <b>{entry.DamageHP} HP</b> / {entry.DamageArmor} Armor ({entry.Hits} hits){hitStr} — {remaining} HP left");
                }
            }

            if (lines.Count > 0)
            {
                var deathData      = _tracker.GetOrCreate(victim.Slot);
                deathData.CenterTimer?.Kill();
                deathData.CenterTimer  = null;
                deathData.CenterMessage = string.Join("<br>", lines);

                deathData.CenterTimer = AddTimer(Config.HUDDisplaySeconds, () =>
                {
                    deathData.CenterMessage = null;
                    deathData.CenterTimer   = null;
                });
            }
        }
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!Config.ShowRoundEndSummary)
        {
            _tracker.ClearRound();
            return HookResult.Continue;
        }

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV
                
                && p.Team > CsTeam.Spectator)
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
            foreach (var player in players)
            {
                if (!_prefs.Get(player.SteamID).SummaryEnabled) continue;

                player.PrintToChat($"{Prefix} {ChatColors.Yellow}{T("leaderboard.header")}");
                for (int i = 0; i < ranked.Count; i++)
                {
                    string medal = i == 0 ? "🥇" : i == 1 ? "🥈" : i == 2 ? "🥉" : $"#{i + 1}";
                    player.PrintToChat(
                        $"{Prefix} {medal} {ChatColors.Green}{ranked[i].Name}{ChatColors.Default} — " +
                        $"{ChatColors.Yellow}{ranked[i].TotalDmg} HP{ChatColors.Default} total");
                }
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

        var taken = data.TakenDamage
            .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
            .OrderByDescending(e => e.Value.DamageHP)
            .ToList();

        if (given.Count == 0 && taken.Count == 0) return;

        data.IsDataShown = true;

        player.PrintToChat($"{Prefix} {ChatColors.Yellow}{T("summary.header")}");

        foreach (var (slot, entry) in given)
        {
            string name      = _tracker.GetName(slot);
            int    remaining = GetRemainingHp(slot);
            string ffTag     = entry.IsFriendlyFire ? $" {ChatColors.Yellow}[FF]{ChatColors.Default}" : "";
            string hitStr    = ChatHitgroupSummary(entry);
            player.PrintToChat(
                $"{Prefix}{ffTag} → {ChatColors.Green}{name}{ChatColors.Default}: " +
                $"{ChatColors.Yellow}{entry.DamageHP} HP{ChatColors.Default} / " +
                $"{ChatColors.LightBlue}{entry.DamageArmor} Armor{ChatColors.Default} " +
                $"({entry.Hits} hit(s)){hitStr} {ChatColors.Default}— " +
                $"{ChatColors.Red}{remaining} HP left{ChatColors.Default}");
        }
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

    private string ChatHitgroupSummary(DamageEntry entry)
    {
        if (!Config.ShowHitgroup || entry.Hitgroups.Count == 0) return "";
        return $" {ChatColors.Default}[{string.Join(", ", entry.Hitgroups.Distinct())}]";
    }

    private string HudHitgroupSummary(DamageEntry entry)
    {
        if (!Config.ShowHitgroup || entry.Hitgroups.Count == 0) return "";
        return $" [{string.Join(", ", entry.Hitgroups.Distinct())}]";
    }

    private int GetRemainingHp(int slot)
    {
        var target = Utilities.GetPlayerFromSlot(slot);
        return target?.PlayerPawn?.Value?.Health ?? 0;
    }
}
