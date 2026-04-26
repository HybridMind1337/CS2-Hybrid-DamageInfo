namespace HybridDamageInfo;

public class DamageEntry
{
    public int DamageHP { get; set; } = 0;
    public int DamageArmor { get; set; } = 0;
    public int Hits { get; set; } = 0;
    public List<string> Hitgroups { get; set; } = new();
    public bool IsFriendlyFire { get; set; } = false;
}

public class RecentDamage
{
    public int TotalDamage { get; set; } = 0;
    public int ArmorDamage { get; set; } = 0;
    public string LastHitgroup { get; set; } = "";
    public DateTime LastDamageTime { get; set; } = DateTime.MinValue;
}

public class PlayerData
{
    public bool IsDataShown { get; set; } = false;
    public int VictimKillerSlot { get; set; } = -1;
    public string? CenterMessage { get; set; } = null;
    public CounterStrikeSharp.API.Modules.Timers.Timer? CenterTimer { get; set; } = null;

    public Dictionary<int, DamageEntry> GivenDamage { get; } = new();
    public Dictionary<int, DamageEntry> TakenDamage { get; } = new();

    public Dictionary<int, RecentDamage> RecentDamages { get; } = new();
}

public class DamageTracker
{
    private readonly Dictionary<int, PlayerData> _data = new();

    private readonly Dictionary<int, string> _nameCache = new();

    public void CacheName(int slot, string name)
    {
        _nameCache[slot] = name;
    }

    public string GetName(int slot) =>
        _nameCache.TryGetValue(slot, out var name) ? name : "Unknown";

    public PlayerData GetOrCreate(int slot)
    {
        if (!_data.TryGetValue(slot, out var data))
        {
            data = new PlayerData();
            _data[slot] = data;
        }
        return data;
    }

    public PlayerData? Get(int slot) =>
        _data.TryGetValue(slot, out var data) ? data : null;

    public IEnumerable<KeyValuePair<int, PlayerData>> All() => _data;

    public void RecordDamage(int attackerSlot, int victimSlot, int hp, int armor, string hitgroup, bool friendlyFire)
    {
        if (attackerSlot == victimSlot) return;

        var attackerData = GetOrCreate(attackerSlot);
        if (!attackerData.GivenDamage.TryGetValue(victimSlot, out var given))
        {
            given = new DamageEntry { IsFriendlyFire = friendlyFire };
            attackerData.GivenDamage[victimSlot] = given;
        }
        given.DamageHP    += hp;
        given.DamageArmor += armor;
        given.Hits++;
        if (!string.IsNullOrEmpty(hitgroup))
            given.Hitgroups.Add(hitgroup);

        var victimData = GetOrCreate(victimSlot);
        if (!victimData.TakenDamage.TryGetValue(attackerSlot, out var taken))
        {
            taken = new DamageEntry { IsFriendlyFire = friendlyFire };
            victimData.TakenDamage[attackerSlot] = taken;
        }
        taken.DamageHP    += hp;
        taken.DamageArmor += armor;
        taken.Hits++;
        if (!string.IsNullOrEmpty(hitgroup))
            taken.Hitgroups.Add(hitgroup);

        if (!attackerData.RecentDamages.TryGetValue(victimSlot, out var recent))
        {
            recent = new RecentDamage();
            attackerData.RecentDamages[victimSlot] = recent;
        }

        if (DateTime.Now - recent.LastDamageTime <= TimeSpan.FromSeconds(5))
            recent.TotalDamage += hp;
        else
        {
            recent.TotalDamage  = hp;
            recent.ArmorDamage  = armor;
        }

        recent.ArmorDamage    = armor;
        recent.LastHitgroup   = hitgroup;
        recent.LastDamageTime = DateTime.Now;
    }

    public void ClearRound()
    {
        _data.Clear();
        _nameCache.Clear();
    }

    public static string HitgroupToString(int hitgroup) => hitgroup switch
    {
        0  => "Generic",
        1  => "Head",
        2  => "Chest",
        3  => "Stomach",
        4  => "Left Arm",
        5  => "Right Arm",
        6  => "Left Leg",
        7  => "Right Leg",
        10 => "Neck",
        _  => "Unknown"
    };
}
