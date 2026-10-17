using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Publishes a Kursy page under Home when that page is missing.
/// It reuses the pillar document type and template used by O mnie.
/// Existing content is not edited. Home's child order is updated only when it differs.
/// </summary>
public class AddCoursesPageMigration : AsyncMigrationBase
{
    public const string PageName = "Kursy";

    /// <summary>
    /// Stable key of the seeded page. A later boot creates its public URL.
    /// </summary>
    public static readonly Guid PageKey = new("c0da7100-0000-4000-8000-000000000006");

    private const string Lead = "Strona w przygotowaniu.";

    private const string Body = "<p><strong>Strona w przygotowaniu.</strong></p>";

    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IContentPublishingService _publishingService;

    public AddCoursesPageMigration(
        IMigrationContext context,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IContentPublishingService publishingService)
        : base(context)
    {
        _contentTypeService = contentTypeService;
        _contentService = contentService;
        _publishingService = publishingService;
    }

    protected override async Task MigrateAsync()
    {
        RebuildCache = true;

        IContent? home = _contentService.GetRootContent()
            .FirstOrDefault(item => item.Trashed is false && item.ContentType.Alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase));
        if (home is null)
        {
            Logger.LogWarning("No Home content node was found, so {Name} was not seeded.", PageName);
            return;
        }

        await EnsureCoursesAsync(home);

        try
        {
            SortHomeChildren(home);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Home children were not reordered.");
        }
    }

    private async Task EnsureCoursesAsync(IContent home)
    {
        // GetById still returns a node that lives in the recycle bin.
        if (_contentService.GetById(PageKey) is not null)
        {
            Logger.LogInformation("{Name} already exists; it was not modified.", PageName);
            return;
        }

        IContent? named = HomeChildren(home)
            .FirstOrDefault(item => string.Equals(item.Name, PageName, StringComparison.Ordinal));
        if (named is not null)
        {
            Logger.LogInformation("A {Name} node already exists under Home; it was not modified.", PageName);
            return;
        }

        IContentType? pillarType = _contentTypeService.Get(BlogAliases.Pillar);
        if (pillarType is null)
        {
            Logger.LogWarning("The pillar document type is missing, so {Name} was not seeded.", PageName);
            return;
        }

        var node = _contentService.Create(PageName, home, pillarType.Alias);
        node.Key = PageKey;
        node.SetValue(BlogAliases.PillarTitle, PageName);
        node.SetValue(BlogAliases.Lead, Lead);
        node.SetValue(BlogAliases.Body, Body);
        node.SetValue(BlogAliases.PillarOrder, 0);
        if (pillarType.DefaultTemplateId > 0)
        {
            node.TemplateId = pillarType.DefaultTemplateId;
        }

        await SaveAndPublishAsync(node);
        Logger.LogInformation("Published pillar {Name}.", PageName);
    }

    private void SortHomeChildren(IContent home)
    {
        List<IContent> children = HomeChildren(home)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToList();
        List<IContent> desired = DesiredOrder(children);
        if (desired.Count != children.Count)
        {
            Logger.LogWarning("Home children were not reordered because the ordered list did not include every child.");
            return;
        }

        if (desired.Select(item => item.Id).SequenceEqual(children.Select(item => item.Id)))
        {
            Logger.LogInformation("Home children already follow the section order.");
            return;
        }

        OperationResult result = _contentService.Sort(desired);
        if (result.Success is false)
        {
            throw new InvalidOperationException($"Sorting Home children failed: {result.Result}.");
        }

        Logger.LogInformation("Sorted Home children as Aktualności, Narzędzia, Kursy, Kategorie, O mnie.");
    }

    private List<IContent> DesiredOrder(List<IContent> children)
    {
        IContent? news = children.FirstOrDefault(item => Alias(item, BlogAliases.Blog));
        IContent? tools = children.FirstOrDefault(item =>
            item.Key == AddToolsCatalogMigration.CatalogKey || Alias(item, BlogAliases.ToolsCatalog));
        IContent? courses = children.FirstOrDefault(item =>
            item.Key == PageKey || string.Equals(item.Name, PageName, StringComparison.Ordinal));
        IContent? categories = children.FirstOrDefault(item => Alias(item, BlogAliases.Categories));
        IContent? about = children.FirstOrDefault(item =>
            item.Key == PillarSeed.AboutKey || string.Equals(item.Name, "O mnie", StringComparison.Ordinal));

        var desired = new List<IContent>();
        foreach (IContent? item in new IContent?[] { news, tools, courses, categories, about })
        {
            if (item is not null && desired.Any(existing => existing.Id == item.Id) is false)
            {
                desired.Add(item);
            }
        }

        foreach (IContent child in children)
        {
            if (desired.Any(existing => existing.Id == child.Id) is false)
            {
                desired.Add(child);
            }
        }

        return desired;
    }

    private List<IContent> HomeChildren(IContent home)
    {
        var children = new List<IContent>();
        const int pageSize = 100;
        long page = 0;
        while (true)
        {
            IEnumerable<IContent> batch = _contentService.GetPagedChildren(home.Id, page, pageSize, out long total);
            children.AddRange(batch.Where(item => item.Trashed is false));
            page++;
            if (page * pageSize >= total)
            {
                return children;
            }
        }
    }

    private async Task SaveAndPublishAsync(IContent content)
    {
        OperationResult saved = _contentService.Save(content);
        if (saved.Success is false)
        {
            throw new InvalidOperationException($"Saving '{content.Name}' failed: {saved.Result}.");
        }

        Attempt<ContentPublishingResult, ContentPublishingOperationStatus> published = await _publishingService.PublishAsync(
            content.Key,
            [new CulturePublishScheduleModel()],
            Constants.Security.SuperUserKey);
        if (published.Success is false)
        {
            throw new InvalidOperationException($"Publishing '{content.Name}' failed: {published.Status}.");
        }
    }

    private static bool Alias(IContent content, string alias)
        => content.ContentType.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase);
}
