using System.Reflection;
using System.Text.Json;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// English titles and summaries for news posts that already exist on the Polish site.
/// Keyed by the Polish node name. The source link stays in the Polish body and is copied across.
/// </summary>
public static class EnglishPostCatalog
{
    public const string ResourceName = "Umbraco.Cms.Web.UI.Blog.english-posts.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static IReadOnlyDictionary<string, EnglishPostCopy> Load()
    {
        Assembly assembly = typeof(EnglishPostCatalog).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");

        EnglishPostCopy[]? items = JsonSerializer.Deserialize<EnglishPostCopy[]>(stream, JsonOptions);
        if (items is null || items.Length == 0)
        {
            throw new InvalidOperationException($"Embedded resource '{ResourceName}' did not contain any posts.");
        }

        var map = new Dictionary<string, EnglishPostCopy>(StringComparer.Ordinal);
        foreach (EnglishPostCopy item in items)
        {
            if (string.IsNullOrWhiteSpace(item.TitlePl) || string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Summary))
            {
                throw new InvalidOperationException("An English post entry is missing its Polish title, English title, or summary.");
            }

            map[item.TitlePl.Trim()] = item;
        }

        return map;
    }
}

/// <summary>
/// One translated news post.
/// </summary>
public sealed class EnglishPostCopy
{
    public string TitlePl { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}
