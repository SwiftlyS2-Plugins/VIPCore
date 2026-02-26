using FluentMigrator;

namespace VIPCore.Database.Migrations;

[Migration(3)]
public class Migration003 : Migration
{
    public override void Up()
    {
        if (!Schema.Table("vip_servers").Column("GUID").Exists())
        {
            Alter.Table("vip_servers")
                .AddColumn("GUID").AsString(36).Nullable();

            Create.UniqueConstraint("UQ_vip_servers_guid")
                .OnTable("vip_servers")
                .Column("GUID");
        }
    }

    public override void Down()
    {
        if (Schema.Table("vip_servers").Column("GUID").Exists())
        {
            Delete.UniqueConstraint("UQ_vip_servers_guid").FromTable("vip_servers");
            Delete.Column("GUID").FromTable("vip_servers");
        }
    }
}
