using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace VIPCore.Config;

public class VipConfig
{
    public string DatabaseConnection { get; set; } = "default";
    public int TimeMode { get; set; } = 0;
    public bool VipLogging { get; set; } = true;
    public int? ServerId { get; set; } = null;
    public bool ShareServerId { get; set; } = false;
    public bool FreezeMenu { get; set; } = false;
    public bool FreezeAdminMenu { get; set; } = false;
}

public class GroupsConfig
{
    public Dictionary<string, VipGroup> Groups { get; set; } = new();

    /// <summary>Finds a group by name, ignoring case like the rest of VIPCore.</summary>
    public (string Name, VipGroup Group)? Find(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        foreach (var (key, group) in Groups)
        {
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
                return (key, group);
        }

        return null;
    }

    public int WeightOf(string? group) => Find(group)?.Group.Weight ?? int.MinValue;
}

public class VipGroup
{
    public int Weight { get; set; } = 0;
    public Dictionary<string, object> Values { get; set; } = new();
    
    /// <summary>
    /// The raw IConfigurationSection for the Values node, used to dynamically
    /// bind feature-specific settings without VIPCore needing to know the schema.
    /// </summary>
    public IConfigurationSection? ValuesSection { get; set; }

    /// <summary>
    /// The feature keys in the order the file lists them under Values. IConfiguration sorts keys,
    /// so this is read from vip_groups.jsonc itself; empty when it could not be.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> FeatureOrder { get; set; } = new();

    // GetChildren() on the live configuration scans every key of every loaded file, and menus ask per feature
    // per row, so the keys are read once per configuration reload. Re-reading on reload keeps following edits.
    private FeatureIndex? _features;

    // The scan this replaced matched nothing for a null key; a dictionary lookup would throw instead.
    public bool HasFeature(string featureKey) => ValuesSection != null
        ? featureKey != null && Features().Entries.ContainsKey(featureKey)
        : Values.ContainsKey(featureKey);

    /// <summary>The feature's settings to bind from, or null when the group does not list the feature.</summary>
    public IConfigurationSection? FeatureSection(string featureKey) =>
        ValuesSection != null && featureKey != null && Features().Entries.TryGetValue(featureKey, out var entry)
            ? entry.Section
            : null;

    private FeatureIndex Features()
    {
        var values = ValuesSection!;
        var index = Volatile.Read(ref _features);
        if (index == null || index.Token.HasChanged || !ReferenceEquals(index.Values, values))
            Volatile.Write(ref _features, index = FeatureIndex.Read(values));
        return index;
    }

    private sealed class FeatureIndex(IConfigurationSection values, IChangeToken token, Dictionary<string, FeatureEntry> entries)
    {
        public IConfigurationSection Values { get; } = values;
        public IChangeToken Token { get; } = token;
        public Dictionary<string, FeatureEntry> Entries { get; } = entries;

        public static FeatureIndex Read(IConfigurationSection values)
        {
            // Taken before reading, so an edit landing mid-read leaves it changed and the next call reads again.
            var token = values.GetReloadToken();
            var entries = new Dictionary<string, FeatureEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in values.GetChildren())
                entries[child.Key] = new FeatureEntry(child);
            return new FeatureIndex(values, token, entries);
        }
    }

    private sealed class FeatureEntry(IConfigurationSection live)
    {
        private IConfigurationSection? _copy;

        // The binder walks children as well, so it gets a copy holding only this feature's keys, at the same path.
        public IConfigurationSection Section => LazyInitializer.EnsureInitialized(ref _copy,
            () => new ConfigurationBuilder().AddInMemoryCollection(KeysOf(live)).Build().GetSection(live.Path));

        // Only the paths a file really holds, as the binder tests them: AsEnumerable() would also add every parent,
        // and the binder reads a path that exists without a value as an explicit null.
        private static IEnumerable<KeyValuePair<string, string?>> KeysOf(IConfigurationSection section)
        {
            var pending = new Stack<IConfigurationSection>();
            pending.Push(section);
            while (pending.TryPop(out var node))
            {
                string? value;
                var exists = node is ConfigurationSection concrete ? concrete.TryGetValue(null, out value) : (value = node.Value) != null;
                if (exists)
                    yield return new(node.Path, value);
                foreach (var child in node.GetChildren())
                    pending.Push(child);
            }
        }
    }
}
