using System.Data;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using VIPCore.Config;

namespace VIPCore.Database;

public class DatabaseConnectionFactory(ISwiftlyCore core, IOptionsMonitor<VipConfig> config)
{
    public IDbConnection CreateConnection()
    {
        return core.Database.GetConnection(config.CurrentValue.DatabaseConnection);
    }
}
