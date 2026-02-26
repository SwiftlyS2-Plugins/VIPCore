using SwiftlyS2.Shared;
using VIPCore.Database;
using VIPCore.Database.Repositories;
using VIPCore.Models;
using VIPCore.Config;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VIPCore.Services;

public class ServerIdentifier
{
    private readonly ISwiftlyCore _core;
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly IUserRepository _userRepository;
    private readonly IOptionsMonitor<VipConfig> _coreConfigMonitor;

    private long _serverId;
    private string? _serverIp;
    private int _serverPort;
    private bool _initialized;

    public long ServerId => _serverId;
    public string? ServerIp => _serverIp;
    public int ServerPort => _serverPort;
    public bool IsInitialized => _initialized;

    public ServerIdentifier(ISwiftlyCore core, DatabaseConnectionFactory connectionFactory, IUserRepository userRepository, IOptionsMonitor<VipConfig> coreConfigMonitor)
    {
        _core = core;
        _connectionFactory = connectionFactory;
        _userRepository = userRepository;
        _coreConfigMonitor = coreConfigMonitor;
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (_coreConfigMonitor.CurrentValue.ServerId.HasValue)
            {
                _serverId = _coreConfigMonitor.CurrentValue.ServerId.Value;
                _serverIp = "configured";
                _serverPort = 0;
                _initialized = true;
                _core.Logger.LogInformation("[VIPCore] Server identified using configured ID {ServerId}.", _serverId);
                return;
            }

            _serverIp = _core.Engine.ServerIP;
            var hostport = _core.ConVar.Find<int>("hostport");

            if (hostport == null || string.IsNullOrEmpty(_serverIp))
            {
                _core.Logger.LogError("[VIPCore] Failed to auto-detect server: Missing hostport or server IP.");
                return;
            }

            _serverPort = hostport.Value;

            if (!await _userRepository.ServerExistsAsync(_serverIp, _serverPort))
            {
                await _userRepository.AddServerAsync(new VipServer
                {
                    serverIp = _serverIp,
                    port = _serverPort
                });
                _core.Logger.LogInformation("[VIPCore] Registered server {IP}:{Port} in database.", _serverIp, _serverPort);
            }

            _serverId = await _userRepository.GetServerIdAsync(_serverIp, _serverPort);
            _initialized = true;

            _core.Logger.LogInformation("[VIPCore] Server identified as ID {ServerId} ({IP}:{Port}).", _serverId, _serverIp, _serverPort);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "[VIPCore] Failed to initialize server identifier.");
        }
    }
}
