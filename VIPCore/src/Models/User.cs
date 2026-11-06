using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace VIPCore.Models;

[System.ComponentModel.DataAnnotations.Schema.Table("vip_users")]
public class User
{
    [System.ComponentModel.DataAnnotations.Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long account_id { get; set; }
    public required string name { get; set; }
    public long lastvisit { get; set; }
    [System.ComponentModel.DataAnnotations.Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long sid { get; set; }
    [System.ComponentModel.DataAnnotations.Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string group { get; set; }
    public long expires { get; set; }
    /// <summary>Unix seconds the grant was made; 0 when it predates VIPCore 1.2.0.</summary>
    public long granted { get; set; }
}

[System.ComponentModel.DataAnnotations.Schema.Table("vip_servers")]
public class VipServer
{
    [System.ComponentModel.DataAnnotations.Key]
    public long serverId { get; set; }
    public string? GUID { get; set; }
    public required string serverIp { get; set; }
    public int port { get; set; }
}
