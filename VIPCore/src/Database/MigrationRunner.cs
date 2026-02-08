using System.Data;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VIPCore.Database;

public static class MigrationRunner
{
    public static void RunMigrations(IDbConnection dbConnection)
    {
        var serviceProvider = new ServiceCollection()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb =>
            {
                ConfigureDatabase(rb, dbConnection);
                rb.ScanIn(typeof(MigrationRunner).Assembly).For.Migrations();
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole())
            .BuildServiceProvider(false);

        using var scope = serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
    }

    private static void ConfigureDatabase(IMigrationRunnerBuilder rb, IDbConnection dbConnection)
    {
        var typeName = dbConnection.GetType().FullName ?? dbConnection.GetType().Name;

        if (typeName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            rb.AddMySql5();
        }
        else if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Postgre", StringComparison.OrdinalIgnoreCase))
        {
            rb.AddPostgres();
        }
        else if (typeName.Contains("SQLite", StringComparison.OrdinalIgnoreCase) || typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            rb.AddSQLite();
        }
        else
        {
            throw new NotSupportedException($"Unsupported database connection type: {typeName}");
        }

        rb.WithGlobalConnectionString(dbConnection.ConnectionString);
    }
}
