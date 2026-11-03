using FluentMigrator;

namespace VIPCore.Database.Migrations;

[Migration(2)]
public class Migration002 : Migration
{
    public override void Up()
    {
        if (!Schema.Table("vip_servers").Exists())
        {
            Create.Table("vip_servers")
                .WithColumn("serverId").AsInt64().NotNullable().PrimaryKey().Identity()
                .WithColumn("serverIp").AsString(45).NotNullable()
                .WithColumn("port").AsInt32().NotNullable()
                .WithColumn("created_at").AsDateTime().WithDefault(SystemMethods.CurrentDateTime)
                .WithColumn("updated_at").AsDateTime().Nullable();
        }
    }

    public override void Down()
    {
        Delete.Table("vip_servers");
    }
}
