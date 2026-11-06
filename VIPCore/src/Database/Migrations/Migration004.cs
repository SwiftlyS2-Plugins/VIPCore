using FluentMigrator;

namespace VIPCore.Database.Migrations;

[Migration(4)]
public class Migration004 : Migration
{
    public override void Up()
    {
        if (!Schema.Table("vip_users").Column("granted").Exists())
        {
            Alter.Table("vip_users")
                .AddColumn("granted").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }

    public override void Down()
    {
        if (Schema.Table("vip_users").Column("granted").Exists())
        {
            Delete.Column("granted").FromTable("vip_users");
        }
    }
}
