namespace HybridDamageInfo;

public class DamageEntry
{
    public int DamageHP { get; set; } = 0;
    public int DamageArmor { get; set; } = 0;
    public int Hits { get; set; } = 0;
    public List<string> Hitgroups { get; set; } = new();
    public bool IsFriendlyFire { get; set; } = false;
}

public class DamageTracker
{
    private readonly Dictionary<ulong, Dictionary<ulong, DamageEntry>> _data = new();
    private readonly Dictionary<ulong, string> _nameCache = new();

    public static ulong GetPlayerId(ulong steamId, int slot, bool isBot) =>
        isBot || steamId == 0 ? (ulong)(slot + 1) * 100_000UL : steamId;

    public void CacheName(ulong id, string name)
    {
        if (id == 0) return;
        _nameCache[id] = name;
    }

    public string GetName(ulong id) =>
        _nameCache.TryGetValue(id, out var name) ? name : $"[unknown]";

    public void RecordDamage(ulong attackerId, ulong targetId, int hp, int armor, int hitgroup, bool friendlyFire)
    {
        if (attackerId == 0 || targetId == 0 || attackerId == targetId) return;

        if (!_data.ContainsKey(attackerId))
            _data[attackerId] = new Dictionary<ulong, DamageEntry>();

        if (!_data[attackerId].ContainsKey(targetId))
            _data[attackerId][targetId] = new DamageEntry { IsFriendlyFire = friendlyFire };

        var entry = _data[attackerId][targetId];
        entry.DamageHP    += hp;
        entry.DamageArmor += armor;
        entry.Hits++;

        string hg = HitgroupToString(hitgroup);
        if (!string.IsNullOrEmpty(hg))
            entry.Hitgroups.Add(hg);
    }

    public Dictionary<ulong, DamageEntry>? GetDamageDealtBy(ulong id) =>
        _data.TryGetValue(id, out var dict) ? dict : null;

    public Dictionary<ulong, DamageEntry> GetDamageReceivedBy(ulong id)
    {
        var result = new Dictionary<ulong, DamageEntry>();
        foreach (var (attackerId, targets) in _data)
            if (targets.TryGetValue(id, out var entry))
                result[attackerId] = entry;
        return result;
    }

    public void Clear()
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
