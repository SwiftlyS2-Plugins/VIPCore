using Microsoft.Extensions.Configuration;

namespace VIPCore.Config;

public class VipConfig
{
    public float Delay { get; set; } = 2.0f;
    public string DatabaseConnection { get; set; } = "default";
    public int TimeMode { get; set; } = 0;
    public int ServerId { get; set; } = 0;
    public bool UseCenterHtmlMenu { get; set; } = true;
    public string ServerIP { get; set; } = "0.0.0.0";
    public int ServerPort { get; set; } = 27015;
    public bool ReOpenMenuAfterItemClick { get; set; } = false;
    public bool VipLogging { get; set; } = true;
}

public class GroupsConfig
{
    public Dictionary<string, VipGroup> Groups { get; set; } = new();
}

public class VipGroup
{
    public Dictionary<string, object> Values { get; set; } = new();
    
    /// <summary>
    /// The raw IConfigurationSection for the Values node, used to dynamically
    /// bind feature-specific settings without VIPCore needing to know the schema.
    /// </summary>
    public IConfigurationSection? ValuesSection { get; set; }
}
