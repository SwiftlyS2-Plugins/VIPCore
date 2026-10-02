using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.ProtobufDefinitions;
using VIPCore.Contract;

namespace VIP_Chat;

[PluginMetadata(Id = "VIP_Chat", Version = "1.0.1", Name = "[VIP] Chat Settings",
    Author = "Pisex / SwiftlyS2 port", Description = "VIP chat prefix and colour settings, native CS2 chat routing.")]
public sealed class VIP_Chat(ISwiftlyCore core) : BasePlugin(core)
{
    private const string Feature = "vip.chat";
    private IVipCoreApiV1? _api;
    private bool _registered, _loaded;
    private Guid _messageHook, _chatHook, _command;
    private ChatModuleConfig _config = new();
    private SettingsStore _store = null!;
    private readonly Dictionary<ulong, DateTime> _pendingInput = new();
    private readonly HashSet<IMenuAPI> _menus = new();
    private readonly Dictionary<ulong, IMenuAPI> _activeMenus = new();
    private int _generation;

    public override void ConfigureSharedInterface(IInterfaceManager manager) { }
    public override void UseSharedInterface(IInterfaceManager manager)
    {
        DetachApi();
        if (!manager.HasSharedInterface("VIPCore.Api.v1"))
        {
            Core.Logger.LogWarning("[VIP_Chat] VIPCore.Api.v1 not found. Install VIPCore v1.1.3 or compatible.");
            return;
        }
        _api = manager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");
        _api.OnCoreReady += Register;
        _api.PlayerRemoved += PlayerRemoved;
        if (_loaded && _api.IsCoreReady()) Register();
    }
    public override void Load(bool hotReload)
    {
        Core.Configuration.InitializeJsonWithModel<ChatModuleConfig>("config.jsonc", "chat");
        var jsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, PropertyNameCaseInsensitive = true
        };
        using var document = JsonDocument.Parse(File.ReadAllText(Core.Configuration.GetConfigPath("config.jsonc")),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        _config = document.RootElement.GetProperty("chat").Deserialize<ChatModuleConfig>(jsonOptions)
            ?? throw new InvalidDataException("Missing chat configuration.");
        _config.MaxPrefixLength = Math.Clamp(_config.MaxPrefixLength, 1, 64);
        _config.InputTimeoutSeconds = Math.Clamp(_config.InputTimeoutSeconds, 10, 300);
        if (_config.Colors == null || _config.Prefixes == null) throw new InvalidDataException("Colors/Prefixes must be objects.");
        _store = new SettingsStore(Path.Combine(Core.PluginDataDirectory, "player_settings.json"));
        _messageHook = Core.NetMessage.HookServerMessage<CUserMessageSayText2>(OnSayText2);
        _chatHook = Core.Command.HookClientChat(OnChatInput);
        _command = Core.Command.RegisterCommand("vipchat", OpenCommand);
        Core.Event.OnClientDisconnected += Disconnected;
        _generation++;
        _loaded = true;
        if (_api?.IsCoreReady() == true) Register();
        Core.Logger.LogInformation("[VIP_Chat] Loaded. Settings: {Path}", Core.PluginDataDirectory);
    }
    private void Register()
    {
        if (!_loaded || _registered || _api == null || !_api.IsCoreReady()) return;
        _api.RegisterFeature(Feature, FeatureType.Selectable, (p, _) =>
        {
            // VIPCore reopens its parent after selectable callbacks. Open two ticks later.
            Later(p.SessionId, OpenMain);
        }, _ => T("Настройки чата", "Налаштування чату", "Chat settings"));
        _registered = true;
    }
    private void DetachApi()
    {
        if (_api == null) return;
        _api.OnCoreReady -= Register;
        _api.PlayerRemoved -= PlayerRemoved;
        if (_registered) _api.UnregisterFeature(Feature);
        _registered = false;
        _api = null;
    }
    private ChatGroupSettings? Group(IPlayer p)
    {
        if (!_loaded || !_registered || !p.IsValid || p.IsFakeClient || p.SteamID == 0 || _api == null
            || !_api.IsCoreReady() || !_api.IsClientVip(p) || !_api.PlayerHasFeature(p, Feature)
            || _api.GetPlayerFeatureState(p, Feature) != FeatureState.Enabled) return null;
        var settings = _api.GetFeatureValue<ChatGroupSettings>(p, Feature);
        if (settings != null)
        {
            settings.Prefix ??= "";
            settings.NameColor ??= "";
            settings.TextColor ??= "";
            settings.PrefixColor ??= "";
        }
        return settings;
    }
    private void OpenCommand(ICommandContext context)
    {
        if (context.Sender is not { } p) return;
        if (Group(p) == null) { Chat(p, T("Нет доступа к настройкам чата.", "Немає доступу до налаштувань чату.", "No access to chat settings.")); return; }
        OpenMain(p);
    }
    private HookResult OnSayText2(CUserMessageSayText2 message)
    {
        // Modify only actual player chat, never server notices or plugin output. Do not resend.
        if (!message.Chat || message.Entityindex <= 0 ||
            !message.Messagename.TrimStart('#').StartsWith("Cstrike_Chat_", StringComparison.Ordinal)) return HookResult.Continue;
        var p = Core.PlayerManager.GetPlayer(message.Entityindex - 1);
        if (p == null || !p.IsValid || p.Controller.Index != (uint)message.Entityindex) return HookResult.Continue;
        var group = Group(p);
        if (group == null) return HookResult.Continue;
        var s = _store.Get(p.SteamID);
        var prefix = ChatFormatting.ResolvePrefix(group.Prefix, s.Prefix, _config);
        var prefixColor = ChatFormatting.ResolveColor(group.PrefixColor, s.PrefixColor, _config);
        var nameColor = ChatFormatting.ResolveColor(group.NameColor, s.NameColor, _config);
        var textColor = ChatFormatting.ResolveColor(group.TextColor, s.TextColor, _config);
        if (prefix.Length == 0 && nameColor.Length == 0 && textColor.Length == 0) return HookResult.Continue;
        if (!NativeChatTemplate.TryBuild(message.Messagename, _config.Language, prefix.Length > 0,
                prefixColor, nameColor, textColor, out var template)) return HookResult.Continue;
        message.Messagename = template;
        if (prefix.Length > 0) message.Param4 = prefix;
        // The original recipient mask, Entityindex, Param1/2/3, Chat and Textallchat stay intact.
        // Prefix uses the unused fourth parameter of the explicitly supported chat templates.
        return HookResult.Continue;
    }
    private string T(string ru, string uk, string en) => (_config.Language ?? "ru").ToLowerInvariant() switch { "uk" or "ua" => uk, "en" => en, _ => ru };
    private static string Safe(string text) => WebUtility.HtmlEncode(text);
    private string Default => T("Обычный", "Звичайний", "Default");
    private void Chat(IPlayer p, string text) => p.SendMessage(MessageType.Chat, "[green][VIP Chat] [default]" + text);
    private IMenuBuilderAPI Builder(string title, bool main = false) => Core.MenusAPI.CreateBuilder().Design.SetMenuTitle(title)
        .SetPlayerFrozen(_config.FreezePlayer).SetSelectButton(KeyBind.E)
        .SetMoveForwardButton(KeyBind.S).SetMoveBackwardButton(KeyBind.W).SetExitButton(KeyBind.R)
        .AddExtraButton(KeyBind.A, T("Назад", "Назад", "Back"), (p, _) => Later(p.SessionId, q =>
        {
            if (main) { CloseOwn(q); q.ExecuteCommand("sw_vip"); }
            else OpenMain(q);
        }));
    private void Button(IMenuBuilderAPI builder, string text, Action<IPlayer> action, bool enabled = true)
    {
        var option = new ButtonMenuOption(text) { Enabled = enabled };
        option.Click += (_, args) =>
        {
            if (Group(args.Player) != null) Later(args.Player.SessionId, action);
            return ValueTask.CompletedTask;
        };
        builder.AddOption(option);
    }
    private void Later(ulong session, Action<IPlayer> action)
    {
        if (!_loaded) return;
        var generation = _generation;
        UiDeferredWork.AfterTwoTicks(Core.Scheduler.NextTick,
            () => _loaded && _generation == generation,
            () =>
            {
                var player = Core.PlayerManager.GetPlayerFromSessionId(session);
                if (player != null && Group(player) != null) action(player);
            });
    }
    private void Show(IPlayer p, IMenuBuilderAPI builder)
    {
        if (Group(p) == null) return;
        var menu = builder.Build();
        _menus.Add(menu);
        Core.MenusAPI.OpenMenuForPlayer(p, menu, (player, closed) =>
        {
            if (_activeMenus.GetValueOrDefault(player.SessionId) == closed) _activeMenus.Remove(player.SessionId);
            Core.Scheduler.NextTick(() => { if (_menus.Remove(closed)) closed.Dispose(); });
        });
        _activeMenus[p.SessionId] = menu;
    }
    private void CloseOwn(IPlayer p)
    {
        if (_activeMenus.Remove(p.SessionId, out var menu)) Core.MenusAPI.CloseMenuForPlayer(p, menu);
    }
    private string ColorLabel(string value)
    {
        if (value.Length == 0) return Default;
        return _config.Colors.FirstOrDefault(x => x.Value.Equals(value, StringComparison.OrdinalIgnoreCase)).Key ?? value;
    }
    private void OpenMain(IPlayer p)
    {
        var g = Group(p); if (g == null) return;
        _pendingInput.Remove(p.SessionId);
        var s = _store.Get(p.SteamID);
        var prefix = ChatFormatting.ResolvePrefix(g.Prefix, s.Prefix, _config);
        var b = Builder(T("Настройки чата", "Налаштування чату", "Chat settings"), main: true);
        Button(b, T("Префикс", "Префікс", "Prefix") + " [" + Safe(prefix.Length == 0 ? Default : prefix) + "]", SelectPrefix, g.Prefix.Length > 0);
        foreach (var field in new[] { "NameColor", "TextColor", "PrefixColor" })
        {
            var f = field;
            var policy = Policy(g, f);
            var label = f switch
            {
                "NameColor" => T("Цвет ника", "Колір ніку", "Name colour"),
                "TextColor" => T("Цвет текста", "Колір тексту", "Text colour"),
                _ => T("Цвет префикса", "Колір префіксу", "Prefix colour")
            };
            Button(b, label + " [" + Safe(ColorLabel(ChatFormatting.ResolveColor(policy, Saved(s, f), _config))) + "]", q => SelectColor(q, f), policy.Length > 0);
        }
        Show(p, b);
    }
    private static string Policy(ChatGroupSettings g, string f) => f switch { "NameColor" => g.NameColor, "TextColor" => g.TextColor, _ => g.PrefixColor };
    private static string? Saved(PlayerChatSettings s, string f) => f switch { "NameColor" => s.NameColor, "TextColor" => s.TextColor, _ => s.PrefixColor };
    private bool Save(IPlayer p, Action<PlayerChatSettings> change)
    {
        if (Group(p) == null) return false;
        try { var s = _store.Get(p.SteamID); change(s); _store.Save(p.SteamID, s); return true; }
        catch (Exception e) { Core.Logger.LogError(e, "[VIP_Chat] Cannot save settings."); Chat(p, T("Не удалось сохранить настройки. Проверьте логи.", "Не вдалося зберегти налаштування. Перевірте логи.", "Cannot save settings. Check server logs.")); return false; }
    }
    private void SelectPrefix(IPlayer p)
    {
        var g = Group(p); if (g == null || g.Prefix.Length == 0) return;
        if (ChatFormatting.IsList(g.Prefix))
        {
            var b = Builder(T("Выберите префикс", "Оберіть префікс", "Choose prefix"));
            Button(b, T("Отключить", "Вимкнути", "Disable"), q => { Save(q, s => s.Prefix = ""); OpenMain(q); });
            foreach (var pair in _config.Prefixes)
            {
                var value = pair.Value;
                Button(b, Safe(pair.Key), q => { var now = Group(q); if (now != null && ChatFormatting.IsList(now.Prefix)) Save(q, s => s.Prefix = value); OpenMain(q); });
            }
            Button(b, T("Назад", "Назад", "Back"), OpenMain); Show(p, b);
        }
        else if (ChatFormatting.IsCustom(g.Prefix))
        {
            var b = Builder(T("Свой префикс", "Власний префікс", "Custom prefix"));
            Button(b, T("Ввести / изменить", "Ввести / змінити", "Enter / change"), q =>
            {
                var now = Group(q); if (now == null || !ChatFormatting.IsCustom(now.Prefix)) return;
                CloseOwn(q); _pendingInput[q.SessionId] = DateTime.UtcNow.AddSeconds(_config.InputTimeoutSeconds);
                Chat(q, T("Напишите префикс в чат. Отмена: !cancel", "Напишіть префікс у чат. Скасувати: !cancel", "Type your prefix in chat. Cancel: !cancel"));
            });
            Button(b, T("Отключить", "Вимкнути", "Disable"), q => { Save(q, s => s.Prefix = ""); OpenMain(q); });
            Button(b, T("Назад", "Назад", "Back"), OpenMain); Show(p, b);
        }
        else
        {
            var s = _store.Get(p.SteamID);
            Save(p, x => x.Prefix = ChatFormatting.ResolvePrefix(g.Prefix, s.Prefix, _config).Length > 0 ? "" : g.Prefix);
            OpenMain(p);
        }
    }
    private void SelectColor(IPlayer p, string field)
    {
        var g = Group(p); if (g == null) return;
        var policy = Policy(g, field); if (policy.Length == 0) return;
        if (!ChatFormatting.IsList(policy))
        {
            var current = ChatFormatting.ResolveColor(policy, Saved(_store.Get(p.SteamID), field), _config);
            SetColor(p, field, current.Length > 0 ? "" : policy); OpenMain(p); return;
        }
        var b = Builder(T("Выберите цвет", "Оберіть колір", "Choose colour"));
        Button(b, T("Отключить", "Вимкнути", "Disable"), q => { SetColor(q, field, ""); OpenMain(q); });
        foreach (var pair in _config.Colors)
        {
            if (ChatFormatting.ColorCode(pair.Value).Length == 0) continue;
            var value = pair.Value;
            Button(b, Safe(pair.Key), q => { var now = Group(q); if (now != null && ChatFormatting.IsList(Policy(now, field))) SetColor(q, field, value); OpenMain(q); });
        }
        Button(b, T("Назад", "Назад", "Back"), OpenMain); Show(p, b);
    }
    private void SetColor(IPlayer p, string field, string value) => Save(p, s =>
    {
        switch (field) { case "NameColor": s.NameColor = value; break; case "TextColor": s.TextColor = value; break; default: s.PrefixColor = value; break; }
    });
    private HookResult OnChatInput(int playerId, string text, bool teamOnly)
    {
        var p = Core.PlayerManager.GetPlayer(playerId);
        if (p == null || !_pendingInput.Remove(p.SessionId, out var deadline)) return HookResult.Continue;
        var g = Group(p);
        if (g == null || !ChatFormatting.IsCustom(g.Prefix)) return HookResult.Continue;
        if (DateTime.UtcNow > deadline)
        {
            Chat(p, T("Время ввода префикса истекло.", "Час введення префіксу минув.", "Prefix input timed out."));
            return HookResult.Continue;
        }
        if (text.Trim().Equals("!cancel", StringComparison.OrdinalIgnoreCase)
            || text.Trim().Equals("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            Chat(p, T("Ввод префикса отменён.", "Введення префіксу скасовано.", "Prefix input cancelled."));
            Later(p.SessionId, OpenMain); return HookResult.Stop;
        }
        // Never swallow ordinary commands as a custom prefix.
        if (text.TrimStart().StartsWith('!') || text.TrimStart().StartsWith('/')) return HookResult.Continue;
        var value = ChatFormatting.CleanPrefix(text, _config.MaxPrefixLength);
        if (value.Length == 0)
        {
            _pendingInput[p.SessionId] = deadline;
            Chat(p, T("Префикс пустой. Повторите ввод или !cancel.", "Префікс порожній. Повторіть або !cancel.", "Empty prefix. Try again or !cancel."));
            return HookResult.Stop;
        }
        if (Save(p, s => s.Prefix = value)) Chat(p, T("Префикс сохранён.", "Префікс збережено.", "Prefix saved."));
        Later(p.SessionId, OpenMain);
        return HookResult.Stop;
    }
    private void PlayerRemoved(IPlayer p, string group) { _pendingInput.Remove(p.SessionId); CloseOwn(p); }
    private void Disconnected(IOnClientDisconnectedEvent e)
    {
        var p = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (p != null) { _pendingInput.Remove(p.SessionId); CloseOwn(p); }
    }
    public override void Unload()
    {
        _loaded = false;
        _generation++;
        Core.NetMessage.Unhook(_messageHook);
        Core.Command.UnhookClientChat(_chatHook);
        Core.Command.UnregisterCommand(_command);
        Core.Event.OnClientDisconnected -= Disconnected;
        foreach (var entry in _activeMenus.ToArray())
        {
            var p = Core.PlayerManager.GetPlayerFromSessionId(entry.Key);
            if (p != null) Core.MenusAPI.CloseMenuForPlayer(p, entry.Value);
        }
        foreach (var menu in _menus.ToArray()) { _menus.Remove(menu); menu.Dispose(); }
        _activeMenus.Clear(); _pendingInput.Clear(); DetachApi();
    }
}
