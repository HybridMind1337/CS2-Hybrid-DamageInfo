namespace HybridDamageInfo;

public class PlayerPreference
{
    public bool ChatEnabled { get; set; } = true;
    public bool HudEnabled { get; set; } = true;
    public bool SummaryEnabled { get; set; } = true;
    public bool MenuOpen { get; set; } = false;
}

public class PlayerPreferenceManager
{
    private readonly Dictionary<ulong, PlayerPreference> _prefs = new();

    public PlayerPreference Get(ulong steamId)
    {
        if (!_prefs.TryGetValue(steamId, out var pref))
        {
            pref = new PlayerPreference();
            _prefs[steamId] = pref;
        }
        return pref;
    }

    public bool Toggle(ulong steamId, string setting)
    {
        var pref = Get(steamId);
        return setting.ToLower() switch
        {
            "chat"    => pref.ChatEnabled    = !pref.ChatEnabled,
            "hud"     => pref.HudEnabled     = !pref.HudEnabled,
            "summary" => pref.SummaryEnabled = !pref.SummaryEnabled,
            _         => false
        };
    }

    public (string Setting, bool NewState) ToggleByNumber(ulong steamId, int number)
    {
        return number switch
        {
            1 => ("chat",    Toggle(steamId, "chat")),
            2 => ("hud",     Toggle(steamId, "hud")),
            3 => ("summary", Toggle(steamId, "summary")),
            _ => ("",        false)
        };
    }

    public void Remove(ulong steamId) => _prefs.Remove(steamId);

    public string BuildMenuHtml(ulong steamId)
    {
        var pref = Get(steamId);

        string On  = "<font color='#00ff88'>ON</font>";
        string Off = "<font color='#ff4444'>OFF</font>";

        string chat    = pref.ChatEnabled    ? On : Off;
        string hud     = pref.HudEnabled     ? On : Off;
        string summary = pref.SummaryEnabled ? On : Off;

        return
            "<b><font color='#ff4444'>[Damage Info]</font> Settings</b><br>" +
            "<br>" +
            $"<b>1.</b> Chat messages — {chat}<br>" +
            $"<b>2.</b> HUD display — {hud}<br>" +
            $"<b>3.</b> Round summary — {summary}<br>" +
            "<br>" +
            "<font color='#aaaaaa'>Type <b>!di 1</b> / <b>!di 2</b> / <b>!di 3</b> to toggle</font>";
    }
}
