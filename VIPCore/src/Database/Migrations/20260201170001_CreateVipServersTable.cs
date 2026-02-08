using FluentMigrator;

namespace VIPCore.Database.Migrations;

[Migration(20260201170001)]
public class CreateVipServersTable : Migration
{
    public override void Up()
    {
        Create.Table("vip_servers")
            .WithColumn("serverId").AsInt64().NotNullable().PrimaryKey().Identity()
            .WithColumn("serverIp").AsString(45).NotNullable()
            .WithColumn("port").AsInt32().NotNullable()
            .WithColumn("created_at").AsDateTime().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("updated_at").AsDateTime().Nullable();
    }

    public override void Down()
    {
        Delete.Table("vip_servers");
    }
}
