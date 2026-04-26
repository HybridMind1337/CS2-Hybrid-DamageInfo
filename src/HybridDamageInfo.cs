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
    public override string ModuleVersion => "3.1.0";
    public override string ModuleAuthor  => "HybridMind";
    public override string ModuleDescription => "Displays damage info after death and round end.";

    public HybridDamageInfoConfig Config { get; set; } = new();

    private readonly DamageTracker           _tracker = new();
    private readonly PlayerPreferenceManager _prefs   = new();
    private Dictionary<string, string>       _lang    = new();

    private string Prefix => $" {ChatColors.Red}[Hybrid-DamageInfo]{ChatColors.Default}";

    public void OnConfigParsed(HybridDamageInfoConfig config)
    {
        Config = config;
        LoadLanguage(config.Language);
    }

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        AddCommand("css_di",         "Hybrid-DamageInfo menu/toggle", OnDiCommand);
        AddCommand("css_damageinfo", "Hybrid-DamageInfo menu/toggle", OnDiCommand);
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

    private static ulong Id(CCSPlayerController p) =>
        DamageTracker.GetPlayerId(p.SteamID, p.Slot, p.IsBot);

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

    private int GetRemainingHp(ulong id)
    {
        var target = Utilities.GetPlayers()
            .FirstOrDefault(p => p.IsValid && DamageTracker.GetPlayerId(p.SteamID, p.Slot, p.IsBot) == id);
        return target?.PlayerPawn?.Value?.Health ?? 0;
    }

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

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        var attacker = @event.Attacker;
        var victim   = @event.Userid;

        if (attacker == null || !attacker.IsValid || victim == null || !victim.IsValid)
            return HookResult.Continue;

        ulong attackerId = Id(attacker);
        ulong victimId   = Id(victim);

        if (attackerId == victimId) return HookResult.Continue;

        if (!Config.ShowBotDamage && (attacker.IsBot || victim.IsBot))
            return HookResult.Continue;

        bool friendlyFire = attacker.TeamNum == victim.TeamNum;
        if (friendlyFire && !Config.ShowFriendlyFire)
            return HookResult.Continue;

        _tracker.CacheName(attackerId, attacker.PlayerName);
        _tracker.CacheName(victimId,   victim.PlayerName);

        _tracker.RecordDamage(attackerId, victimId, @event.DmgHealth, @event.DmgArmor, @event.Hitgroup, friendlyFire);

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var victim = @event.Userid;
        if (victim == null || !victim.IsValid) return HookResult.Continue;

        ulong victimId = Id(victim);
        var   pref     = _prefs.Get(victim.SteamID);

        var receivedDamage = _tracker.GetDamageReceivedBy(victimId);
        var dealtDamage    = _tracker.GetDamageDealtBy(victimId);

        if (Config.ShowChatMessage && pref.ChatEnabled)
        {
            if (Config.CompactDeathMessage)
            {
                var killer = receivedDamage
                    .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
                    .OrderByDescending(e => e.Value.DamageHP)
                    .FirstOrDefault();

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

                if (dealtDamage != null)
                {
                    var significant = dealtDamage
                        .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
                        .ToList();

                    if (significant.Count > 0)
                    {
                        int totalHp = significant.Sum(e => e.Value.DamageHP);
                        int targets = significant.Count;
                        victim.PrintToChat(
                            $"{Prefix} You dealt {ChatColors.Yellow}{totalHp} HP{ChatColors.Default} " +
                            $"across {ChatColors.Green}{targets} player(s){ChatColors.Default}" +
                            $"{ChatColors.Default} — details in round summary");
                    }
                }
            }
            else
            {
                // Full mode
                foreach (var (attackerId, entry) in receivedDamage)
                {
                    if (entry.DamageHP < Config.MinDamageToShow) continue;
                    string name   = _tracker.GetName(attackerId);
                    string ffTag  = entry.IsFriendlyFire ? $" {ChatColors.Yellow}[FF]{ChatColors.Default}" : "";
                    string hitStr = ChatHitgroupSummary(entry);
                    victim.PrintToChat(
                        $"{Prefix}{ffTag} {ChatColors.LightRed}{name}{ChatColors.Default} dealt " +
                        $"{ChatColors.Yellow}{entry.DamageHP} HP{ChatColors.Default} / " +
                        $"{ChatColors.LightBlue}{entry.DamageArmor} Armor{ChatColors.Default} " +
                        $"({entry.Hits} hit(s)){hitStr}");
                }

                if (dealtDamage != null)
                {
                    foreach (var (targetId, entry) in dealtDamage)
                    {
                        if (entry.DamageHP < Config.MinDamageToShow) continue;
                        string name      = _tracker.GetName(targetId);
                        int    remaining = GetRemainingHp(targetId);
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
        }

        if (Config.ShowConsoleLog)
        {
            foreach (var (attackerId, entry) in receivedDamage)
            {
                if (entry.DamageHP < Config.MinDamageToShow) continue;
                string name = _tracker.GetName(attackerId);
                string ff   = entry.IsFriendlyFire ? " [FF]" : "";
                victim.PrintToConsole($"[Hybrid-DamageInfo]{ff} {name} dealt {entry.DamageHP} HP / {entry.DamageArmor} Armor ({entry.Hits} hits)");
            }
        }

        if (Config.ShowCenterHUD && pref.HudEnabled)
        {
            var lines = new List<string>();

            var sigReceived = receivedDamage
                .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
                .OrderByDescending(e => e.Value.DamageHP)
                .ToList();

            var sigDealt = dealtDamage?
                .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
                .OrderByDescending(e => e.Value.DamageHP)
                .ToList();

            if (sigReceived.Count > 0)
            {
                lines.Add($"<b>{T("received.header")}</b>");
                foreach (var (attackerId, entry) in sigReceived)
                {
                    string name   = _tracker.GetName(attackerId);
                    string ffTag  = entry.IsFriendlyFire ? " <font color='#ffcc00'>[FF]</font>" : "";
                    string hitStr = HudHitgroupSummary(entry);
                    lines.Add($"{name}{ffTag}: <b>{entry.DamageHP} HP</b> / {entry.DamageArmor} Armor ({entry.Hits} hits){hitStr}");
                }
            }

            if (sigDealt != null && sigDealt.Count > 0)
            {
                lines.Add($"<b>{T("dealt.header")}</b>");
                foreach (var (targetId, entry) in sigDealt)
                {
                    string name      = _tracker.GetName(targetId);
                    int    remaining = GetRemainingHp(targetId);
                    string hitStr    = HudHitgroupSummary(entry);
                    lines.Add($"{name}: <b>{entry.DamageHP} HP</b> / {entry.DamageArmor} Armor ({entry.Hits} hits){hitStr} — {remaining} HP left");
                }
            }

            if (lines.Count > 0)
                victim.PrintToCenterHtml(string.Join("<br>", lines), (int)Config.HUDDisplaySeconds);
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (!Config.ShowRoundEndSummary)
        {
            _tracker.Clear();
            return HookResult.Continue;
        }

        var leaderboard = new List<(string Name, int TotalDmg)>();

        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot) continue;

            ulong playerId = Id(player);
            var   pref     = _prefs.Get(player.SteamID);
            var   dealt    = _tracker.GetDamageDealtBy(playerId);

            if (dealt == null || dealt.Count == 0) continue;

            var significant = dealt
                .Where(e => e.Value.DamageHP >= Config.MinDamageToShow)
                .OrderByDescending(e => e.Value.DamageHP)
                .ToList();

            int totalDmg = significant.Sum(e => e.Value.DamageHP);
            if (totalDmg > 0)
                leaderboard.Add((player.PlayerName, totalDmg));

            if (!pref.SummaryEnabled || significant.Count == 0) continue;

            player.PrintToChat($"{Prefix} {ChatColors.Yellow}{T("summary.header")}");
            foreach (var (targetId, entry) in significant)
            {
                string name   = _tracker.GetName(targetId);
                string ffTag  = entry.IsFriendlyFire ? $" {ChatColors.Yellow}[FF]{ChatColors.Default}" : "";
                string hitStr = ChatHitgroupSummary(entry);
                player.PrintToChat(
                    $"{Prefix}{ffTag} → {ChatColors.Green}{name}{ChatColors.Default}: " +
                    $"{ChatColors.Yellow}{entry.DamageHP} HP{ChatColors.Default} / " +
                    $"{ChatColors.LightBlue}{entry.DamageArmor} Armor{ChatColors.Default} " +
                    $"({entry.Hits} hit(s)){hitStr}");
            }
        }

        // Leaderboard
        if (leaderboard.Count > 0)
        {
            var ranked = leaderboard.OrderByDescending(t => t.TotalDmg).ToList();
            foreach (var player in Utilities.GetPlayers())
            {
                if (!player.IsValid || player.IsBot) continue;
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

        _tracker.Clear();
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _tracker.Clear();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
            _prefs.Remove(player.SteamID);
        return HookResult.Continue;
    }
}
