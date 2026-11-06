using System.Text.Json;

namespace VIPCore.Config;

internal static class GroupFeatureOrder
{
    /// <summary>
    /// Maps each group to its Values keys in file order. Mirrors the group search in VIPCore.Load:
    /// any "Groups" object, first definition of a group wins.
    /// </summary>
    public static Dictionary<string, List<string>> Read(string path)
    {
        var order = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        using var document = JsonDocument.Parse(File.ReadAllText(path), options);
        Collect(document.RootElement, order);
        return order;
    }

    private static void Collect(JsonElement element, Dictionary<string, List<string>> order)
    {
        if (element.ValueKind != JsonValueKind.Object) return;

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals("Groups", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var group in property.Value.EnumerateObject())
                {
                    if (group.Value.ValueKind != JsonValueKind.Object || order.ContainsKey(group.Name)) continue;

                    foreach (var field in group.Value.EnumerateObject())
                    {
                        if (field.Name.Equals("Values", StringComparison.OrdinalIgnoreCase) && field.Value.ValueKind == JsonValueKind.Object)
                            order[group.Name] = field.Value.EnumerateObject().Select(value => value.Name).ToList();
                    }
                }
            }

            Collect(property.Value, order);
        }
    }
}
