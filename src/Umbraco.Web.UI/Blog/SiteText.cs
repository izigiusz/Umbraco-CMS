using System.Globalization;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// UI strings for the Codation portal. Dictionary items use the same keys.
/// A missing dictionary value falls back to the string for the current culture.
/// </summary>
public static class SiteText
{
    public const string Polish = "pl";
    public const string English = "en";

    /// <summary>
    /// Culture and Hostnames entry. A leading slash is a host-independent path,
    /// so /en works on umbraco.runasp.net and on any other host.
    /// </summary>
    public const string EnglishDomain = "/en";

    /// <summary>
    /// Explicit production host. The path domain <see cref="EnglishDomain" /> still works on any host.
    /// </summary>
    public const string ProductionHost = "umbraco.runasp.net";

    public const string NavLabel = "Codation.Nav.Label";
    public const string LangLabel = "Codation.Lang.Label";
    public const string BrandTagline = "Codation.Brand.Tagline";
    public const string FooterBlurb = "Codation.Footer.Blurb";
    public const string FooterSections = "Codation.Footer.Sections";
    public const string FooterCategories = "Codation.Footer.Categories";
    public const string MetaDescription = "Codation.Meta.Description";
    public const string HomeEyebrow = "Codation.Home.Eyebrow";
    public const string HomeTagline = "Codation.Home.Tagline";
    public const string HomeLatest = "Codation.Home.Latest";
    public const string HomeEmpty = "Codation.Home.Empty";
    public const string HomeAllNews = "Codation.Home.AllNews";
    public const string HomeFeatured = "Codation.Home.Featured";
    public const string HomePostFailed = "Codation.Home.PostFailed";
    public const string HomeItemFailed = "Codation.Home.ItemFailed";
    public const string HomeCategories = "Codation.Home.Categories";
    public const string PillarEyebrow = "Codation.Pillar.Eyebrow";
    public const string PillarUnavailable = "Codation.Pillar.Unavailable";
    public const string PillarRelated = "Codation.Pillar.Related";
    public const string ToolsCategory = "Codation.Tools.Category";
    public const string ToolsShowAll = "Codation.Tools.ShowAll";
    public const string ToolsEmpty = "Codation.Tools.Empty";
    public const string ToolsFailed = "Codation.Tools.Failed";
    public const string ToolsUntitled = "Codation.Tools.Untitled";
    public const string BlogCategory = "Codation.Blog.Category";
    public const string BlogTag = "Codation.Blog.Tag";
    public const string BlogEmpty = "Codation.Blog.Empty";
    public const string BlogNewer = "Codation.Blog.Newer";
    public const string BlogOlder = "Codation.Blog.Older";
    public const string BlogPager = "Codation.Blog.Pager";
    public const string BlogName = "Codation.Blog.Name";
    public const string CategoriesEmpty = "Codation.Categories.Empty";
    public const string CategoryPosts = "Codation.Category.Posts";
    public const string CategoryEmpty = "Codation.Category.Empty";
    public const string CategoryTools = "Codation.Category.Tools";
    public const string PostUntitled = "Codation.Post.Untitled";
    public const string NavNews = "Codation.Nav.News";
    public const string NavTools = "Codation.Nav.Tools";
    public const string NavCourses = "Codation.Nav.Courses";
    public const string NavCategories = "Codation.Nav.Categories";
    public const string NavAbout = "Codation.Nav.About";

    private static readonly Dictionary<string, (string Polish, string English)> Values = new(StringComparer.Ordinal)
    {
        [NavLabel] = ("Główne", "Main"),
        [LangLabel] = ("Język", "Language"),
        [BrandTagline] = ("Od kodowania z AI do Software Factories", "From coding with AI to Software Factories"),
        [FooterBlurb] = (
            "Codation zbiera wiedzę o tym, jak projektować systemy, w których agenci pomagają tworzyć i dostarczać oprogramowanie.",
            "Codation collects knowledge about designing systems in which agents help build and deliver software."),
        [FooterSections] = ("Działy", "Sections"),
        [FooterCategories] = ("Kategorie", "Categories"),
        [MetaDescription] = (
            "Codation. AI SDLC | Harness Engineering | Software Factory",
            "Codation. AI SDLC | Harness Engineering | Software Factory"),
        [HomeEyebrow] = ("Inżynieria oprogramowania natywna dla AI", "AI-native software engineering"),
        [HomeTagline] = ("Od kodowania z AI do Software Factories.", "From coding with AI to Software Factories."),
        [HomeLatest] = ("Najnowsze wpisy", "Latest posts"),
        [HomeEmpty] = ("Brak wpisów.", "No posts yet."),
        [HomeAllNews] = ("Wszystkie aktualności", "All news"),
        [HomeFeatured] = ("Polecane tematy", "Featured topics"),
        [HomePostFailed] = ("Nie udało się wyświetlić wpisu.", "This post could not be displayed."),
        [HomeItemFailed] = ("Nie udało się wyświetlić pozycji.", "This item could not be displayed."),
        [HomeCategories] = ("Kategorie", "Categories"),
        [PillarEyebrow] = ("Dział", "Section"),
        [PillarUnavailable] = ("Treść tej strony jest niedostępna.", "This page's content is unavailable."),
        [PillarRelated] = ("Powiązane kategorie", "Related categories"),
        [ToolsCategory] = ("Kategoria:", "Category:"),
        [ToolsShowAll] = ("Pokaż wszystkie", "Show all"),
        [ToolsEmpty] = ("Brak narzędzi.", "No tools."),
        [ToolsFailed] = ("Nie udało się wyświetlić narzędzia.", "This tool could not be displayed."),
        [ToolsUntitled] = ("Bez nazwy", "Untitled"),
        [BlogCategory] = ("Kategoria:", "Category:"),
        [BlogTag] = ("Tag:", "Tag:"),
        [BlogEmpty] = ("Brak wpisów.", "No posts yet."),
        [BlogNewer] = ("Nowsze", "Newer"),
        [BlogOlder] = ("Starsze", "Older"),
        [BlogPager] = ("Wpisy", "Posts"),
        [BlogName] = ("Aktualności", "News"),
        [CategoriesEmpty] = ("Brak kategorii.", "No categories."),
        [CategoryPosts] = ("Wpisy", "Posts"),
        [CategoryEmpty] = ("Brak wpisów w tej kategorii.", "No posts in this category."),
        [CategoryTools] = ("Narzędzia", "Tools"),
        [PostUntitled] = ("Bez tytułu", "Untitled"),
        [NavNews] = ("Aktualności", "News"),
        [NavTools] = ("Narzędzia", "Tools"),
        [NavCourses] = ("Kursy", "Courses"),
        [NavCategories] = ("Kategorie", "Categories"),
        [NavAbout] = ("O mnie", "About me"),
    };

    /// <summary>
    /// True only for the public English culture <c>en</c>.
    /// The install language <c>en-US</c> is not the public English site.
    /// </summary>
    public static bool IsEnglish => string.Equals(RequestCulture(), English, StringComparison.OrdinalIgnoreCase);

    public static string HtmlLang => IsEnglish ? English : Polish;

    public static string OgLocale => IsEnglish ? "en_US" : "pl_PL";

    /// <summary>
    /// Culture used to format dates and dictionary fallbacks on the public site.
    /// </summary>
    public static CultureInfo DisplayCulture
        => CultureInfo.GetCultureInfo(IsEnglish ? "en-GB" : "pl-PL");

    private static string? RequestCulture()
    {
        try
        {
            var culture = StaticServiceProvider.Instance.GetRequiredService<IVariationContextAccessor>().VariationContext?.Culture;
            if (string.IsNullOrWhiteSpace(culture) is false)
            {
                return culture;
            }
        }
        catch (Exception)
        {
            // Views still render. Polish is the public default.
        }

        var ui = CultureInfo.CurrentUICulture.Name;
        return ui.Equals(English, StringComparison.OrdinalIgnoreCase) ? English : null;
    }

    public static IEnumerable<(string Key, string Polish, string English)> DictionaryEntries()
    {
        foreach (KeyValuePair<string, (string Polish, string English)> pair in Values)
        {
            yield return (pair.Key, pair.Value.Polish, pair.Value.English);
        }
    }

    /// <summary>
    /// Dictionary value, or the in-code string for the current culture.
    /// </summary>
    public static string Get(UmbracoHelper umbraco, string key)
    {
        try
        {
            var value = umbraco.GetDictionaryValue(key, DisplayCulture);
            if (string.IsNullOrWhiteSpace(value) is false)
            {
                return value;
            }
        }
        catch (Exception)
        {
            // The page still renders from the in-code strings.
        }

        return Fallback(key);
    }

    /// <summary>
    /// Both language strings. Migrations must not use <see cref="Fallback"/>,
    /// because the server culture is not the culture being written.
    /// </summary>
    public static (string Polish, string English) ValuesFor(string key)
        => Values.TryGetValue(key, out (string Polish, string English) pair) ? pair : (string.Empty, string.Empty);

    /// <summary>
    /// In-code string for the current culture. Used where no Umbraco helper is in scope.
    /// </summary>
    public static string Fallback(string key)
    {
        if (Values.TryGetValue(key, out (string Polish, string English) pair) is false)
        {
            return string.Empty;
        }

        return IsEnglish ? pair.English : pair.Polish;
    }
}
