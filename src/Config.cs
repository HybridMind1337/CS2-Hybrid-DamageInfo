using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace HybridDamageInfo;

public class HybridDamageInfoConfig : BasePluginConfig
{
    [JsonPropertyName("Language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("ShowCenterHUD")]
    public bool ShowCenterHUD { get; set; } = true;

    [JsonPropertyName("ShowChatMessage")]
    public bool ShowChatMessage { get; set; } = true;

    [JsonPropertyName("ShowConsoleLog")]
    public bool ShowConsoleLog { get; set; } = false;

    [JsonPropertyName("ShowRoundEndSummary")]
    public bool ShowRoundEndSummary { get; set; } = true;

    [JsonPropertyName("ShowHitgroup")]
    public bool ShowHitgroup { get; set; } = true;

    [JsonPropertyName("HUDDisplaySeconds")]
    public float HUDDisplaySeconds { get; set; } = 3.0f;

    [JsonPropertyName("ShowBotDamage")]
    public bool ShowBotDamage { get; set; } = true;

    [JsonPropertyName("ShowFriendlyFire")]
    public bool ShowFriendlyFire { get; set; } = true;

    [JsonPropertyName("MinDamageToShow")]
    public int MinDamageToShow { get; set; } = 20;

    [JsonPropertyName("CompactDeathMessage")]
    public bool CompactDeathMessage { get; set; } = true;
}
