using Microsoft.Extensions.Configuration;

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

    public bool HasFeature(string featureKey) => ValuesSection != null
        ? ValuesSection.GetChildren().Any(c => c.Key.Equals(featureKey, StringComparison.OrdinalIgnoreCase))
        : Values.ContainsKey(featureKey);
}
