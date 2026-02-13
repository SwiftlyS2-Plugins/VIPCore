using FluentMigrator;

namespace VIPCore.Database.Migrations;

[Migration(3)]
public class Migration003 : Migration
{
    public override void Up()
    {
        if (!Schema.Table("vip_users").Exists())
        {
            // Fresh installation - create table with new schema
            Create.Table("vip_users")
                .WithColumn("steam_id").AsInt64().NotNullable()
                .WithColumn("name").AsString(64).NotNullable()
                .WithColumn("last_visit").AsDateTime().NotNullable()
                .WithColumn("sid").AsInt64().NotNullable()
                .WithColumn("group").AsString(64).NotNullable()
                .WithColumn("expires").AsDateTime().NotNullable();

            Create.PrimaryKey("PK_vip_users").OnTable("vip_users").Columns("steam_id", "sid", "group");
        }
        else
        {
            // Migration from old structure to new structure
            // Step 1: Check if this is the old schema by checking for account_id column
            if (Schema.Table("vip_users").Column("account_id").Exists())
            {
                // Step 2: Rename existing table to backup
                Rename.Table("vip_users").To("vip_users_backup");

                // Step 3: Create new table with updated schema
                Create.Table("vip_users")
                    .WithColumn("steam_id").AsInt64().NotNullable()
                    .WithColumn("name").AsString(64).NotNullable()
                    .WithColumn("last_visit").AsDateTime().NotNullable()
                    .WithColumn("sid").AsInt64().NotNullable()
                    .WithColumn("group").AsString(64).NotNullable()
                    .WithColumn("expires").AsDateTime().NotNullable();

                Create.PrimaryKey("PK_vip_users").OnTable("vip_users").Columns("steam_id", "sid", "group");

                // Step 4: Migrate data from backup to new table with conversions
                // account_id is already SteamId64, so direct mapping to steam_id
                // Convert lastvisit (Unix timestamp) to last_visit (DateTime)
                // Convert expires (Unix timestamp, 0 = permanent) to expires (DateTime, MinValue = permanent)
                Execute.Sql(@"
                    INSERT INTO vip_users (steam_id, name, last_visit, sid, `group`, expires)
                    SELECT 
                        account_id AS steam_id,
                        name,
                        FROM_UNIXTIME(lastvisit) AS last_visit,
                        sid,
                        `group`,
                        CASE 
                            WHEN expires = 0 THEN '0001-01-01 00:00:00'
                            ELSE FROM_UNIXTIME(expires)
                        END AS expires
                    FROM vip_users_backup
                ");
            }
        }
    }

    public override void Down()
    {
        if (Schema.Table("vip_users").Exists())
        {
            // Step 1: Rename current table to backup
            Rename.Table("vip_users").To("vip_users_backup");

            // Step 2: Create old schema table
            Create.Table("vip_users")
                .WithColumn("account_id").AsInt64().NotNullable()
                .WithColumn("name").AsString(64).NotNullable()
                .WithColumn("lastvisit").AsInt64().NotNullable()
                .WithColumn("sid").AsInt64().NotNullable()
                .WithColumn("group").AsString(64).NotNullable()
                .WithColumn("expires").AsInt64().NotNullable();

            Create.PrimaryKey("PK_vip_users").OnTable("vip_users").Columns("account_id", "sid", "group");

            // Step 3: Migrate data back from new schema to old schema
            // steam_id is already SteamId64, so direct mapping back to account_id
            // Convert last_visit (DateTime) back to lastvisit (Unix timestamp)
            // Convert expires (DateTime, MinValue = permanent) back to expires (Unix timestamp, 0 = permanent)
            Execute.Sql(@"
                INSERT INTO vip_users (account_id, name, lastvisit, sid, `group`, expires)
                SELECT 
                    steam_id AS account_id,
                    name,
                    UNIX_TIMESTAMP(last_visit) AS lastvisit,
                    sid,
                    `group`,
                    CASE 
                        WHEN expires = '0001-01-01 00:00:00' THEN 0
                        ELSE UNIX_TIMESTAMP(expires)
                    END AS expires
                FROM vip_users_backup
            ");
        }
    }
}
