using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace VIPCore.Models;

[System.ComponentModel.DataAnnotations.Schema.Table("vip_users")]
public class User
{
    [System.ComponentModel.DataAnnotations.Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ulong steam_id { get; set; }
    public required string name { get; set; }
    public DateTime last_visit { get; set; }
    [System.ComponentModel.DataAnnotations.Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long sid { get; set; }
    [System.ComponentModel.DataAnnotations.Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required string group { get; set; }
    public DateTime expires { get; set; }
}

[System.ComponentModel.DataAnnotations.Schema.Table("vip_servers")]
public class VipServer
{
    [System.ComponentModel.DataAnnotations.Key]
    public long serverId { get; set; }
    public required string serverIp { get; set; }
    public int port { get; set; }
}
