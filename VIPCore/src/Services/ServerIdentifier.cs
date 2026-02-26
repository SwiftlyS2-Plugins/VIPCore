using SwiftlyS2.Shared;
using VIPCore.Database;
using VIPCore.Database.Repositories;
using VIPCore.Models;
using VIPCore.Config;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;

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
    private string _serverGuid = string.Empty;

    public long ServerId => _serverId;
    public string? ServerIp => _serverIp;
    public int ServerPort => _serverPort;
    public bool IsInitialized => _initialized;
    public string ServerGuid => _serverGuid;

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
            var configuredServerId = _coreConfigMonitor.CurrentValue.ServerId;
            if (configuredServerId.HasValue)
            {
                _core.Logger.LogInformation("[VIPCore] Server identified using configured ID {ServerId}.", configuredServerId.Value);
            }

            if (string.IsNullOrWhiteSpace(_core.PluginDataDirectory))
            {
                _core.Logger.LogError("[VIPCore] Cannot initialize server GUID: PluginDataDirectory is empty.");
                return;
            }

            Directory.CreateDirectory(_core.PluginDataDirectory);

            var guidPath = Path.Combine(_core.PluginDataDirectory, "server_id.txt");

            if (!File.Exists(guidPath))
            {
                _serverGuid = Guid.NewGuid().ToString();
                try
                {
                    File.WriteAllText(guidPath, _serverGuid);
                }
                catch (Exception ioEx)
                {
                    _core.Logger.LogError(ioEx, "[VIPCore] Failed to write server GUID file at {Path}.", guidPath);
                    return;
                }
                _core.Logger.LogWarning("[VIPCore] Generated new Server GUID: {Guid}", _serverGuid);
            }
            else
            {
                try
                {
                    _serverGuid = File.ReadAllText(guidPath).Trim();
                }
                catch (Exception ioEx)
                {
                    _core.Logger.LogError(ioEx, "[VIPCore] Failed to read server GUID file at {Path}.", guidPath);
                    return;
                }
            }

            if (!Guid.TryParse(_serverGuid, out _))
            {
                _serverGuid = Guid.NewGuid().ToString();
                try
                {
                    File.WriteAllText(guidPath, _serverGuid);
                }
                catch (Exception ioEx)
                {
                    _core.Logger.LogError(ioEx, "[VIPCore] Failed to rewrite invalid server GUID file at {Path}.", guidPath);
                    return;
                }
                _core.Logger.LogWarning("[VIPCore] Invalid Server GUID detected. Generated new GUID: {Guid}", _serverGuid);
            }

            var guidServer = await _userRepository.GetServerByGuidAsync(_serverGuid);
            if (guidServer != null)
            {
                _serverIp = guidServer.serverIp;
                _serverPort = guidServer.port;

                if (configuredServerId.HasValue && guidServer.serverId != configuredServerId.Value)
                {
                    var desiredId = configuredServerId.Value;

                    var desiredServer = await _userRepository.GetServerByIdAsync(desiredId);
                    if (desiredServer != null)
                    {
                        desiredServer.GUID = _serverGuid;
                        desiredServer.serverIp = _serverIp;
                        desiredServer.port = _serverPort;
                        await _userRepository.UpdateServerAsync(desiredServer);
                        await _userRepository.ClearGuidFromOtherServersAsync(_serverGuid, desiredId);

                        _serverId = desiredId;
                        _initialized = true;
                        _core.Logger.LogInformation("[VIPCore] Synced server GUID {Guid} to configured ID {ServerId} ({IP}:{Port}).", _serverGuid, _serverId, _serverIp, _serverPort);
                        return;
                    }

                    var moved = await _userRepository.TryMoveServerIdAsync(guidServer.serverId, desiredId);
                    if (moved)
                    {
                        await _userRepository.ClearGuidFromOtherServersAsync(_serverGuid, desiredId);
                        _serverId = desiredId;
                        _initialized = true;
                        _core.Logger.LogInformation("[VIPCore] Moved server GUID {Guid} to configured ID {ServerId} ({IP}:{Port}).", _serverGuid, _serverId, _serverIp, _serverPort);
                        return;
                    }

                    var inserted = await _userRepository.TryInsertServerWithIdAsync(new VipServer
                    {
                        serverId = desiredId,
                        GUID = _serverGuid,
                        serverIp = _serverIp,
                        port = _serverPort
                    });
                    if (inserted)
                    {
                        await _userRepository.ClearGuidFromOtherServersAsync(_serverGuid, desiredId);
                        _serverId = desiredId;
                        _initialized = true;
                        _core.Logger.LogInformation("[VIPCore] Inserted configured server ID {ServerId} for GUID {Guid} ({IP}:{Port}).", _serverId, _serverGuid, _serverIp, _serverPort);
                        return;
                    }

                    _core.Logger.LogWarning("[VIPCore] Found GUID {Guid} in database but could not sync to configured server ID {ServerId}. Continuing with DB ID {DbServerId}.", _serverGuid, desiredId, guidServer.serverId);
                }

                _serverId = configuredServerId ?? guidServer.serverId;
                _initialized = true;

                _core.Logger.LogInformation("[VIPCore] Server registered in database using GUID {Guid} ({IP}:{Port}).", _serverGuid, _serverIp, _serverPort);
                return;
            }

            string? detectedIp = null;
            int detectedPort = 0;
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                detectedIp = _core.Engine.ServerIP;
                var hostport = _core.ConVar.Find<int>("hostport");
                detectedPort = hostport?.Value ?? 0;

                if (!string.IsNullOrEmpty(detectedIp) && detectedPort > 0)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            if (string.IsNullOrEmpty(detectedIp) || detectedPort <= 0)
            {
                _core.Logger.LogError("[VIPCore] Failed to register server using GUID {Guid}: Missing hostport or server IP.", _serverGuid);
                if (configuredServerId.HasValue)
                {
                    _serverId = configuredServerId.Value;
                    _serverIp = null;
                    _serverPort = 0;
                    _initialized = true;
                }
                return;
            }

            _serverIp = detectedIp;
            _serverPort = detectedPort;

            var ipPortServer = await _userRepository.GetServerByIpPortAsync(_serverIp, _serverPort);
            if (ipPortServer != null)
            {
                if (string.IsNullOrEmpty(ipPortServer.GUID))
                {
                    ipPortServer.GUID = _serverGuid;
                    await _userRepository.UpdateServerAsync(ipPortServer);
                }

                if (configuredServerId.HasValue && ipPortServer.serverId != configuredServerId.Value)
                {
                    var desiredId = configuredServerId.Value;

                    var desiredServer = await _userRepository.GetServerByIdAsync(desiredId);
                    if (desiredServer != null)
                    {
                        desiredServer.GUID = _serverGuid;
                        desiredServer.serverIp = _serverIp;
                        desiredServer.port = _serverPort;
                        await _userRepository.UpdateServerAsync(desiredServer);
                        await _userRepository.ClearGuidFromOtherServersAsync(_serverGuid, desiredId);

                        _serverId = desiredId;
                        _initialized = true;
                        _core.Logger.LogInformation("[VIPCore] Synced server {IP}:{Port} (GUID {Guid}) to configured ID {ServerId}.", _serverIp, _serverPort, _serverGuid, _serverId);
                        return;
                    }

                    var moved = await _userRepository.TryMoveServerIdAsync(ipPortServer.serverId, desiredId);
                    if (moved)
                    {
                        await _userRepository.ClearGuidFromOtherServersAsync(_serverGuid, desiredId);
                        _serverId = desiredId;
                        _initialized = true;
                        _core.Logger.LogInformation("[VIPCore] Moved server {IP}:{Port} (GUID {Guid}) to configured ID {ServerId}.", _serverIp, _serverPort, _serverGuid, _serverId);
                        return;
                    }

                    var inserted = await _userRepository.TryInsertServerWithIdAsync(new VipServer
                    {
                        serverId = desiredId,
                        GUID = _serverGuid,
                        serverIp = _serverIp,
                        port = _serverPort
                    });
                    if (inserted)
                    {
                        await _userRepository.ClearGuidFromOtherServersAsync(_serverGuid, desiredId);
                        _serverId = desiredId;
                        _initialized = true;
                        _core.Logger.LogInformation("[VIPCore] Inserted configured server ID {ServerId} for {IP}:{Port} (GUID {Guid}).", _serverId, _serverIp, _serverPort, _serverGuid);
                        return;
                    }

                    _core.Logger.LogWarning("[VIPCore] Linked GUID {Guid} to {IP}:{Port} but could not sync to configured server ID {ServerId}. Continuing with DB ID {DbServerId}.", _serverGuid, _serverIp, _serverPort, desiredId, ipPortServer.serverId);
                }

                _serverId = configuredServerId ?? ipPortServer.serverId;
                _initialized = true;
                _core.Logger.LogInformation("[VIPCore] Server registered in database ({IP}:{Port}) and linked to GUID {Guid}.", _serverIp, _serverPort, _serverGuid);
                return;
            }

            await _userRepository.AddServerAsync(new VipServer
            {
                GUID = _serverGuid,
                serverIp = _serverIp,
                port = _serverPort
            });

            guidServer = await _userRepository.GetServerByGuidAsync(_serverGuid);
            if (guidServer == null)
            {
                _core.Logger.LogError("[VIPCore] Server registration succeeded but GUID lookup failed for {Guid}.", _serverGuid);
                return;
            }

            _serverId = configuredServerId ?? guidServer.serverId;
            _initialized = true;

            _core.Logger.LogInformation("[VIPCore] Registered server GUID {Guid} in database ({IP}:{Port}).", _serverGuid, _serverIp, _serverPort);
        }
        catch (Exception ex)
        {
            _core.Logger.LogError(ex, "[VIPCore] Failed to initialize server identifier.");
        }
    }
}
