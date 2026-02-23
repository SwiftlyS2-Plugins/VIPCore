using Microsoft.Extensions.Configuration;

namespace VIPCore.Config;

public class VipConfig
{
    public string DatabaseConnection { get; set; } = "default";
    public int TimeMode { get; set; } = 0;
    public bool VipLogging { get; set; } = true;
    public int? ServerId { get; set; } = null;
}

public class GroupsConfig
{
    public Dictionary<string, VipGroup> Groups { get; set; } = new();
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
}
