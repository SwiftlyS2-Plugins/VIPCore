using System.Text.Json;

namespace VIP_Chat;

public sealed class SettingsStore
{
    private readonly string _path;
    private Dictionary<string, PlayerChatSettings> _players;
    public SettingsStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Fail rather than silently overwriting a corrupt settings file.
        _players = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, PlayerChatSettings>>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Settings file contains null.")
            : new();
        if (_players.Values.Any(x => x == null))
            throw new InvalidDataException("Settings file contains a null player record.");
    }
    public PlayerChatSettings Get(ulong steamId) => _players.TryGetValue(steamId.ToString(), out var value)
        ? value.Copy() : new();
    public void Save(ulong steamId, PlayerChatSettings settings)
    {
        if (steamId == 0) throw new InvalidOperationException("SteamID is not authorized yet.");
        var next = new Dictionary<string, PlayerChatSettings>(_players) { [steamId.ToString()] = settings.Copy() };
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
        _players = next;
    }
}
