using System.Globalization;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Read-only helpers for the Codation portal templates.
/// A missing or broken property returns a fallback instead of throwing.
/// </summary>
public static class Portal
{
    private static readonly Dictionary<string, string> AccentBySlug = new(StringComparer.OrdinalIgnoreCase)
    {
        ["agenci-kodowania"] = "#22d3ee",
        ["modele"] = "#38bdf8",
        ["orkiestracja"] = "#2dd4bf",
        ["rag"] = "#67e8f9",
        ["evaluation-i-reliability"] = "#818cf8",
        ["agentic-sdlc"] = "#34d399",
        ["ci-cd-i-pipeliney"] = "#fbbf24",
        ["observability-i-monitoring"] = "#fb7185",
        ["playbooks"] = "#a78bfa",
        ["neo4j"] = "#06b6d4",
    };

    /// <summary>
    /// Published pillar pages under Home, in editorial order.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> Pillars(IPublishedContent? home)
    {
        try
        {
            IEnumerable<IPublishedContent>? pages = home?.ChildrenOfType(BlogAliases.Pillar);
            if (pages is null)
            {
                return [];
            }

            return pages.OrderBy(PillarOrder).ThenBy(page => page.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Header and footer sections, left to right:
    /// Aktualności, Narzędzia, Kursy, Kategorie, O mnie.
    /// A missing page is skipped. Lookup failures return an empty list.
    /// </summary>
    public static IReadOnlyList<MainSection> MainSections(IPublishedContent? current)
    {
        try
        {
            IPublishedContent? home = current?.Root();
            IPublishedContent? news = FirstOfType(home, BlogAliases.Blog);
            IPublishedContent? tools = ToolsCatalog(home);
            IPublishedContent? courses = FindPillar(home, AddCoursesPageMigration.PageKey, AddCoursesPageMigration.PageName);
            IPublishedContent? categories = Container(current ?? home);
            IPublishedContent? about = FindPillar(home, PillarSeed.AboutKey, "O mnie");

            var sections = new List<MainSection>(5);
            AddSection(sections, news, SiteText.Fallback(SiteText.NavNews), current is not null && IsNews(current));
            AddSection(sections, tools, SiteText.Fallback(SiteText.NavTools), current is not null && IsAlias(current, BlogAliases.ToolsCatalog));
            AddSection(sections, courses, SiteText.Fallback(SiteText.NavCourses), IsPage(current, courses));
            AddSection(sections, categories, SiteText.Fallback(SiteText.NavCategories), current is not null && IsCategories(current));
            AddSection(sections, about, SiteText.Fallback(SiteText.NavAbout), IsPage(current, about));
            return sections;
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Published categories under the Kategorie container.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> Categories(IPublishedContent? anyPage)
    {
        try
        {
            IPublishedContent? container = Container(anyPage);
            IEnumerable<IPublishedContent>? items = container?.ChildrenOfType(BlogAliases.Category);
            return items?.ToList() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// The published tools list under Home, when it exists.
    /// </summary>
    public static IPublishedContent? ToolsCatalog(IPublishedContent? anyPage)
    {
        try
        {
            IEnumerable<IPublishedContent>? pages = anyPage?.Root()?.ChildrenOfType(BlogAliases.ToolsCatalog);
            return pages?.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Published tools under the list, ordered by name.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> Tools(IPublishedContent? catalog)
    {
        try
        {
            IEnumerable<IPublishedContent>? items = catalog?.ChildrenOfType(BlogAliases.Tool);
            if (items is null)
            {
                return [];
            }

            return items.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Published tools whose picked category is this document, ordered by name.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> ToolsInCategory(IPublishedContent? category)
    {
        if (category is null)
        {
            return [];
        }

        var result = new List<IPublishedContent>();
        foreach (IPublishedContent tool in Tools(ToolsCatalog(category)))
        {
            IPublishedContent? picked = ToolCategory(tool);
            if (picked is not null && picked.Key == category.Key)
            {
                result.Add(tool);
            }
        }

        return result;
    }

    /// <summary>
    /// Category picked on a tool. Unpublished and trashed items are omitted.
    /// </summary>
    public static IPublishedContent? ToolCategory(IPublishedContent? tool)
    {
        if (tool is null || tool.HasProperty(BlogAliases.ToolCategory) is false)
        {
            return null;
        }

        try
        {
            IPublishedContent? single = tool.Value<IPublishedContent>(BlogAliases.ToolCategory);
            if (IsCategory(single))
            {
                return single;
            }

            IEnumerable<IPublishedContent>? many = tool.Value<IEnumerable<IPublishedContent>>(BlogAliases.ToolCategory);
            if (many is null)
            {
                return null;
            }

            foreach (IPublishedContent item in many)
            {
                if (IsCategory(item))
                {
                    return item;
                }
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Returns whether the tool's picked category uses this slug.
    /// </summary>
    public static bool MatchesToolCategory(IPublishedContent? tool, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        IPublishedContent? picked = ToolCategory(tool);
        if (picked is null)
        {
            return false;
        }

        var itemSlug = Text(picked, BlogAliases.Slug, null);
        return string.Equals(itemSlug, slug.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Absolute http(s) address of a tool. Anything else is omitted.
    /// </summary>
    public static string? ToolUrl(IPublishedContent? tool)
    {
        var raw = Text(tool, BlogAliases.ToolUrl, null);
        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri) is false)
        {
            return null;
        }

        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) is false
            && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) is false)
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    /// <summary>
    /// Short description shown on the tools list.
    /// </summary>
    public static string? ToolDescription(IPublishedContent? tool)
    {
        var description = Text(tool, BlogAliases.ShortDescription, null);
        return string.IsNullOrWhiteSpace(description) ? null : description;
    }

    /// <summary>
    /// The categories container, when it is published.
    /// </summary>
    public static IPublishedContent? Container(IPublishedContent? anyPage)
    {
        try
        {
            IEnumerable<IPublishedContent>? containers = anyPage?.Root()?.ChildrenOfType(BlogAliases.Categories);
            return containers?.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Categories picked on a pillar. Empty when the picker is blank.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> RelatedCategories(IPublishedContent? pillar)
    {
        var result = new List<IPublishedContent>();
        if (pillar is null || pillar.HasProperty(BlogAliases.RelatedCategories) is false)
        {
            return result;
        }

        try
        {
            IEnumerable<IPublishedContent>? many = pillar.Value<IEnumerable<IPublishedContent>>(BlogAliases.RelatedCategories);
            if (many is not null)
            {
                foreach (IPublishedContent item in many)
                {
                    if (IsCategory(item))
                    {
                        result.Add(item);
                    }
                }

                return result;
            }

            IPublishedContent? single = pillar.Value<IPublishedContent>(BlogAliases.RelatedCategories);
            if (IsCategory(single) && single is not null)
            {
                result.Add(single);
            }
        }
        catch (Exception)
        {
            return result;
        }

        return result;
    }

    /// <summary>
    /// Newest posts whose picked category is this document.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> PostsInCategory(IPublishedContent? category, int take = 24)
    {
        if (category is null)
        {
            return [];
        }

        return PostsMatching(category, take, post =>
        {
            IPublishedContent? picked = BlogList.PickedCategory(post);
            return picked is not null && picked.Key == category.Key;
        });
    }

    /// <summary>
    /// Newest posts in any category picked on the pillar.
    /// </summary>
    public static IReadOnlyList<IPublishedContent> PostsForPillar(IPublishedContent? pillar, int take = 6)
    {
        HashSet<Guid> keys = RelatedCategories(pillar).Select(item => item.Key).ToHashSet();
        if (keys.Count == 0 || pillar is null)
        {
            return [];
        }

        return PostsMatching(pillar, take, post =>
        {
            IPublishedContent? picked = BlogList.PickedCategory(post);
            return picked is not null && keys.Contains(picked.Key);
        });
    }

    /// <summary>
    /// Display name of a category document.
    /// </summary>
    public static string CategoryName(IPublishedContent? category)
        => Text(category, BlogAliases.CategoryName, category?.Name);

    /// <summary>
    /// A text property, or an empty string when it is missing.
    /// </summary>
    public static string TextOrEmpty(IPublishedContent? content, string alias)
        => Text(content, alias, null);

    /// <summary>
    /// Title of a pillar, falling back to the node name.
    /// </summary>
    public static string PillarTitle(IPublishedContent? pillar)
        => Text(pillar, BlogAliases.PillarTitle, pillar?.Name);

    /// <summary>
    /// Optional lead text.
    /// </summary>
    public static string? Lead(IPublishedContent? content)
    {
        var lead = Text(content, BlogAliases.Lead, null);
        return string.IsNullOrWhiteSpace(lead) ? null : lead;
    }

    /// <summary>
    /// CSS color for a category chip. Custom colors are accepted only as hex.
    /// </summary>
    public static string AccentCss(IPublishedContent? category)
    {
        if (TryHex(Text(category, BlogAliases.AccentColor, null), out var custom))
        {
            return custom;
        }

        var slug = Text(category, BlogAliases.Slug, null);
        if (string.IsNullOrWhiteSpace(slug) is false && AccentBySlug.TryGetValue(slug, out var mapped))
        {
            return mapped;
        }

        return "#22d3ee";
    }

    /// <summary>
    /// A calendar date in the current request culture.
    /// </summary>
    public static string FormatDate(DateTime value)
        => value.ToString("d MMMM yyyy", SiteText.DisplayCulture);

    /// <summary>
    /// Public URL of this page in another culture, or the fallback when that culture is not published.
    /// </summary>
    public static string UrlForCulture(IPublishedContent? content, string culture, string fallback)
    {
        if (content is null)
        {
            return fallback;
        }

        try
        {
            var url = content.Url(culture);
            if (string.IsNullOrWhiteSpace(url) || url == "#")
            {
                return fallback;
            }

            return url;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static IPublishedContent? FirstOfType(IPublishedContent? home, string alias)
        => home?.ChildrenOfType(alias)?.FirstOrDefault();

    private static IPublishedContent? FindPillar(IPublishedContent? home, Guid key, string name)
    {
        IEnumerable<IPublishedContent>? pages = home?.ChildrenOfType(BlogAliases.Pillar);
        if (pages is null)
        {
            return null;
        }

        IPublishedContent? byName = null;
        foreach (IPublishedContent page in pages)
        {
            if (page.Key == key)
            {
                return page;
            }

            if (byName is null && string.Equals(page.Name, name, StringComparison.Ordinal))
            {
                byName = page;
            }
        }

        return byName;
    }

    private static void AddSection(List<MainSection> sections, IPublishedContent? page, string fallback, bool current)
    {
        if (page is null)
        {
            return;
        }

        var label = fallback;
        try
        {
            if (IsAlias(page, BlogAliases.Pillar))
            {
                label = PillarTitle(page);
            }
            else if (string.IsNullOrWhiteSpace(page.Name) is false)
            {
                label = page.Name;
            }
        }
        catch (Exception)
        {
            label = fallback;
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            label = fallback;
        }

        var url = "#";
        try
        {
            var resolved = page.Url();
            if (string.IsNullOrWhiteSpace(resolved) is false)
            {
                url = resolved;
            }
        }
        catch (Exception)
        {
            url = "#";
        }

        sections.Add(new MainSection(label, url, current));
    }

    private static bool IsNews(IPublishedContent page)
        => IsAlias(page, BlogAliases.Blog) || IsAlias(page, BlogAliases.Post);

    private static bool IsCategories(IPublishedContent page)
        => IsAlias(page, BlogAliases.Categories) || IsAlias(page, BlogAliases.Category);

    private static bool IsPage(IPublishedContent? current, IPublishedContent? page)
        => current is not null && page is not null && current.Id == page.Id;

    private static bool IsAlias(IPublishedContent page, string alias)
        => page.ContentType.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase);

    private static int PillarOrder(IPublishedContent page)
    {
        try
        {
            int? number = page.Value<int?>(BlogAliases.PillarOrder);
            if (number.HasValue)
            {
                return number.Value;
            }

            var raw = page.Value<string>(BlogAliases.PillarOrder);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        catch (Exception)
        {
            return page.SortOrder;
        }

        return page.SortOrder;
    }

    private static IReadOnlyList<IPublishedContent> PostsMatching(IPublishedContent anyPage, int take, Func<IPublishedContent, bool> match)
    {
        var result = new List<IPublishedContent>();
        try
        {
            IEnumerable<IPublishedContent>? blogs = anyPage.Root()?.ChildrenOfType(BlogAliases.Blog);
            IPublishedContent? blog = blogs?.FirstOrDefault();
            IEnumerable<IPublishedContent>? posts = blog?.ChildrenOfType(BlogAliases.Post);
            if (posts is null)
            {
                return result;
            }

            foreach (IPublishedContent post in posts)
            {
                if (match(post))
                {
                    result.Add(post);
                }
            }
        }
        catch (Exception)
        {
            return result;
        }

        if (take < 1)
        {
            take = 1;
        }

        return result.OrderByDescending(BlogList.PublishDate).Take(take).ToList();
    }

    private static string Text(IPublishedContent? content, string alias, string? fallback)
    {
        if (content is null)
        {
            return fallback ?? string.Empty;
        }

        try
        {
            if (content.HasProperty(alias))
            {
                var value = content.Value<string>(alias);
                if (string.IsNullOrWhiteSpace(value) is false)
                {
                    return value.Trim();
                }
            }
        }
        catch (Exception)
        {
            return fallback ?? string.Empty;
        }

        return fallback ?? string.Empty;
    }

    private static bool IsCategory(IPublishedContent? item)
        => item is not null && item.ContentType.Alias.Equals(BlogAliases.Category, StringComparison.OrdinalIgnoreCase);

    private static bool TryHex(string? raw, out string hex)
    {
        hex = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        if (value.StartsWith('#') is false)
        {
            value = "#" + value;
        }

        if (value.Length is not 4 and not 7)
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (Uri.IsHexDigit(value[index]) is false)
            {
                return false;
            }
        }

        hex = value.ToLowerInvariant();
        return true;
    }
}

/// <summary>
/// One link in the header and in the footer sections column.
/// </summary>
/// <param name="Label">Visible name.</param>
/// <param name="Url">Public URL, or # when the page has none.</param>
/// <param name="Current">True when this request is that section.</param>
public sealed record MainSection(string Label, string Url, bool Current);
