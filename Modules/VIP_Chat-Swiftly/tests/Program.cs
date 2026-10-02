using VIP_Chat;
using System.Text;
using System.Text.Json;
int checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("FAIL: " + label); }
var c = new ChatModuleConfig();
Check(ChatFormatting.IsList("LIST"), "list case insensitive");
Check(ChatFormatting.IsCustom("Custom"), "custom case insensitive");
Check(!ChatFormatting.IsList("blacklist"), "no mode substring match");
foreach (var token in c.Colors.Values) Check(ChatFormatting.ColorCode(token).Length == 1, "supported " + token);
Check(ChatFormatting.ColorCode("{RED}") == "\u0007", "red code");
Check(ChatFormatting.ColorCode("[green]") == "\u0004", "Swiftly-style colour alias");
Check(ChatFormatting.ColorCode("{invalid}") == "", "invalid colour is not injected");
Check(ChatFormatting.CleanPrefix("[Kyiv Light]", 32) == "[Kyiv Light]", "literal prefix brackets retained");
Check(ChatFormatting.CleanPrefix("\u0007{RED}[green]<X>\"\\\n", 32) == "X", "injection stripped");
Check(ChatFormatting.CleanPrefix("😀😀😀", 2).EnumerateRunes().Count() == 2, "Unicode rune limit");
Check(ChatFormatting.ResolvePrefix("list", "[VIP]", c) == "[VIP]", "list prefix permitted");
Check(ChatFormatting.ResolvePrefix("list", "[not-allowed]", c) == "", "saved custom prefix cannot escape list access");
Check(ChatFormatting.ResolvePrefix("list", null, c) == "", "list starts unset");
Check(ChatFormatting.ResolvePrefix("list", "", c) == "", "list disabled");
Check(ChatFormatting.ResolvePrefix("[VIP]", null, c) == "[VIP]", "fixed prefix initial default");
Check(ChatFormatting.ResolvePrefix("[VIP]", "[other]", c) == "[VIP]", "fixed group ignores other saved prefix");
Check(ChatFormatting.ResolvePrefix("[VIP]", "", c) == "", "fixed prefix disable survives reconnect");
Check(ChatFormatting.ResolvePrefix("", "[VIP]", c) == "", "no prefix entitlement");
Check(ChatFormatting.ResolvePrefix("custom", "Test", c) == "Test", "custom prefix permitted");
Check(ChatFormatting.ResolveColor("list", "{RED}", c) == "{RED}", "list colour permitted");
Check(ChatFormatting.ResolveColor("list", "{ORANGE}", c) == "", "not-in-palette colour forbidden");
Check(ChatFormatting.ResolveColor("{RED}", null, c) == "{RED}", "fixed colour initial default");
Check(ChatFormatting.ResolveColor("{RED}", "{GREEN}", c) == "{RED}", "fixed colour ignores other saved colour");
Check(ChatFormatting.ResolveColor("{RED}", "", c) == "", "fixed colour disable retained");
Check(ChatFormatting.ResolveColor("", "{RED}", c) == "", "no colour entitlement");
foreach (var key in new[] { "Cstrike_Chat_All", "Cstrike_Chat_AllDead", "Cstrike_Chat_AllSpec",
    "Cstrike_Chat_CT", "Cstrike_Chat_T", "Cstrike_Chat_CT_Dead", "Cstrike_Chat_T_Dead",
    "Cstrike_Chat_CT_Loc", "Cstrike_Chat_T_Loc", "Cstrike_Chat_Spec" })
{
    Check(NativeChatTemplate.TryBuild("#" + key, "ru", true, "{LIGHTPURPLE}", "{LIME}", "{GREEN}", out var template), "supported native template " + key);
    Check(template.StartsWith(" \u0001"), "CS2 direct line has leading space and default control");
    Check(template.Contains("\u0003%s4 \u0006%s1"), "prefix and name colours in FORMAT, not params");
    Check(template.EndsWith("\u0004%s2"), "text colour in FORMAT, not params");
    Check(!template.Contains("[green]") && !template.Contains("[lightpurple]"), "Helper.Colored resolved tags");
    Check(template.Contains("%s3") == key.EndsWith("_Loc"), "location remains a client parameter");
    Check(template.Contains("*МЁРТВ*") == key.Contains("Dead"), "dead marker retained");
}
Check(!NativeChatTemplate.TryBuild("#Cstrike_Chat_NewUnknownVariant", "ru", true, "", "", "", out _), "unknown templates not rewritten");
Check(NativeChatTemplate.TryBuild("Cstrike_Chat_All", "en", false, "{RED}", "", "", out var noPrefix)
    && !noPrefix.Contains("%s4") && !noPrefix.Contains("\u0007"), "no orphan prefix colour");
Check(NativeChatTemplate.TryBuild("Cstrike_Chat_AllDead", "uk", true, "[red]", "[lime]", "[green]", out var uk)
    && uk.Contains("*МЕРТВИЙ*") && uk.Contains("\u0007%s4"), "Ukrainian labels and Swiftly-style colours");
Check(ChatFormatting.ColorCode("{BLUE}") == ChatFormatting.ColorCode("[blue]"), "legacy blue matches Swiftly native blue");
var queue = new Queue<Action>(); var ran = 0; var active = true;
UiDeferredWork.AfterTwoTicks(queue.Enqueue, () => active, () => ran++);
Check(ran == 0 && queue.Count == 1, "deferred UI does not run inline");
queue.Dequeue()(); Check(ran == 0 && queue.Count == 1, "first tick only queues second tick");
queue.Dequeue()(); Check(ran == 1 && queue.Count == 0, "second tick runs action once");
UiDeferredWork.AfterTwoTicks(queue.Enqueue, () => active, () => ran++);
active = false; queue.Dequeue()(); Check(ran == 1 && queue.Count == 0, "unload before first tick suppresses work");
active = true; UiDeferredWork.AfterTwoTicks(queue.Enqueue, () => active, () => ran++);
queue.Dequeue()(); active = false; queue.Dequeue()(); Check(ran == 1, "unload before second tick suppresses work");
var generation = 1; var capturedGeneration = generation;
UiDeferredWork.AfterTwoTicks(queue.Enqueue, () => generation == capturedGeneration, () => ran++);
generation++; queue.Dequeue()(); Check(ran == 1 && queue.Count == 0, "hot reload invalidates previous generation");
c.Prefixes.Remove("VIP");
Check(ChatFormatting.ResolvePrefix("list", "[VIP]", c) == "", "removed prefix stops applying");
c.Colors.Remove("Красный");
Check(ChatFormatting.ResolveColor("list", "{RED}", c) == "", "removed colour stops applying");
var dir = Path.Combine(Path.GetTempPath(), "vip-chat-tests-" + Guid.NewGuid());
try
{
    var path = Path.Combine(dir, "settings.json");
    var store = new SettingsStore(path);
    Check(store.Get(76561198000000001).Prefix == null, "new SteamID starts unset");
    store.Save(76561198000000001, new() { Prefix = "", NameColor = "{LIME}", TextColor = "{GREEN}" });
    var reloaded = new SettingsStore(path);
    Check(reloaded.Get(76561198000000001).Prefix == "", "disabled persists across reload");
    Check(reloaded.Get(76561198000000001).NameColor == "{LIME}", "colour persists across reload");
    Check(reloaded.Get(76561198000000002).NameColor == null, "different SteamID isolated");
    var copy = reloaded.Get(76561198000000001); copy.NameColor = "hack";
    Check(reloaded.Get(76561198000000001).NameColor == "{LIME}", "returned settings are copied");
    Check(!File.Exists(path + ".tmp"), "atomic save leaves no temporary file");
    var threw = false; try { reloaded.Save(0, new()); } catch (InvalidOperationException) { threw = true; }
    Check(threw, "unauthorized SteamID cannot persist settings");
    File.WriteAllText(path, "bad json"); threw = false;
    try { _ = new SettingsStore(path); } catch (JsonException) { threw = true; }
    Check(threw && File.ReadAllText(path) == "bad json", "corrupt data not silently overwritten");
    File.WriteAllText(path, "{\"76561198000000001\":null}"); threw = false;
    try { _ = new SettingsStore(path); } catch (InvalidDataException) { threw = true; }
    Check(threw, "null records rejected");
}
finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
Console.WriteLine($"PASS: {checks} assertions (native colour templates, deferred UI lifecycle, entitlement policies, sanitization, persistence).");
