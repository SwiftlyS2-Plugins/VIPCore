using FluentMigrator;

namespace VIPCore.Database.Migrations;

[Migration(20260201170000)]
public class CreateVipUsersTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table("vip_users").Exists())
        {
            Create.Table("vip_users")
                .WithColumn("account_id").AsInt64().NotNullable()
                .WithColumn("name").AsString(64).NotNullable()
                .WithColumn("lastvisit").AsInt64().NotNullable()
                .WithColumn("sid").AsInt64().NotNullable()
                .WithColumn("group").AsString(64).NotNullable()
                .WithColumn("expires").AsInt64().NotNullable();

            Create.PrimaryKey("PK_vip_users").OnTable("vip_users").Columns("account_id", "sid");
        }
    }

    public override void Down()
    {
        Delete.Table("vip_users");
    }
}
