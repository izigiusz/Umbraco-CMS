using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Moves the retired pillar pages and categories to the recycle bin, adds Neo4j and O mnie,
/// and renames the existing Blog node to Aktualności. Existing posts and featured picks are not edited.
/// </summary>
public class RestructurePortalMigration : AsyncMigrationBase
{
    public const string NewsName = "Aktualności";

    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IContentPublishingService _publishingService;

    public RestructurePortalMigration(
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

        foreach (CategorySeedItem item in CategorySeed.Items.Where(CategorySeed.IsRetired))
        {
            TrashIfPresent(item.Key, item.Name);
        }

        foreach (PillarSeedItem item in PillarSeed.Items.Where(PillarSeed.IsRetired))
        {
            TrashIfPresent(item.Key, item.Title);
        }

        IContent? home = _contentService.GetRootContent()
            .FirstOrDefault(item => item.ContentType.Alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase) && item.Trashed is false);
        if (home is null)
        {
            Logger.LogWarning("No Home content node was found, so Aktualności, Neo4j and O mnie were not updated.");
            return;
        }

        await RenameBlogAsync(home);
        await EnsureNeo4jAsync();
        await EnsureAboutAsync(home);
    }

    private void TrashIfPresent(Guid key, string label)
    {
        IContent? content = _contentService.GetById(key);
        if (content is null)
        {
            Logger.LogInformation("{Label} ({Key}) is missing; it was not trashed.", label, key);
            return;
        }

        if (content.Trashed)
        {
            Logger.LogInformation("{Label} is already in the recycle bin.", label);
            return;
        }

        OperationResult result = _contentService.MoveToRecycleBin(content);
        if (result.Success is false)
        {
            throw new InvalidOperationException($"Moving '{label}' to the recycle bin failed: {result.Result}.");
        }

        Logger.LogInformation("Moved {Label} to the recycle bin.", label);
    }

    private async Task RenameBlogAsync(IContent home)
    {
        IContent? blog = _contentService.GetPagedChildren(home.Id, 0, 100, out _)
            .FirstOrDefault(item => item.Trashed is false && item.ContentType.Alias.Equals(BlogAliases.Blog, StringComparison.OrdinalIgnoreCase));
        if (blog is null)
        {
            Logger.LogWarning("No Blog node was found, so it was not renamed to {Name}.", NewsName);
            return;
        }

        if (string.Equals(blog.Name, NewsName, StringComparison.Ordinal))
        {
            Logger.LogInformation("The news node is already named {Name}; posts were not changed.", NewsName);
            return;
        }

        blog.Name = NewsName;
        await SaveAndPublishAsync(blog);
        Logger.LogInformation("Renamed the blog node to {Name}. Posts were not changed.", NewsName);
    }

    private async Task EnsureNeo4jAsync()
    {
        if (_contentService.GetById(CategorySeed.Neo4jKey) is not null)
        {
            Logger.LogInformation("Neo4j already exists; it was not modified.");
            return;
        }

        IContent? container = _contentService.GetById(CategorySeed.ContainerKey);
        IContentType? categoryType = _contentTypeService.Get(BlogAliases.Category);
        if (container is null || container.Trashed || categoryType is null)
        {
            Logger.LogWarning("The category container is missing, so Neo4j was not seeded.");
            return;
        }

        CategorySeedItem item = CategorySeed.Items.Single(entry => entry.Key == CategorySeed.Neo4jKey);
        var node = _contentService.Create(item.Name, container, categoryType.Alias);
        node.Key = item.Key;
        node.SetValue(BlogAliases.CategoryName, item.Name);
        node.SetValue(BlogAliases.Slug, item.Slug);
        if (string.IsNullOrWhiteSpace(item.Description) is false)
        {
            node.SetValue(BlogAliases.Description, item.Description);
        }

        if (categoryType.DefaultTemplateId > 0)
        {
            node.TemplateId = categoryType.DefaultTemplateId;
        }

        await SaveAndPublishAsync(node);
        Logger.LogInformation("Published category Neo4j.");
    }

    private async Task EnsureAboutAsync(IContent home)
    {
        if (_contentService.GetById(PillarSeed.AboutKey) is not null)
        {
            Logger.LogInformation("O mnie already exists; it was not modified.");
            return;
        }

        IContentType? pillarType = _contentTypeService.Get(BlogAliases.Pillar);
        PillarSeedItem? item = PillarSeed.Items.SingleOrDefault(entry => entry.Key == PillarSeed.AboutKey);
        if (pillarType is null || item is null)
        {
            Logger.LogWarning("The pillar document type is missing, so O mnie was not seeded.");
            return;
        }

        var node = _contentService.Create(item.Title, home, pillarType.Alias);
        node.Key = item.Key;
        node.SetValue(BlogAliases.PillarTitle, item.Title);
        node.SetValue(BlogAliases.Lead, item.Lead);
        node.SetValue(BlogAliases.Body, item.Body);
        node.SetValue(BlogAliases.PillarOrder, item.Order);
        if (pillarType.DefaultTemplateId > 0)
        {
            node.TemplateId = pillarType.DefaultTemplateId;
        }

        await SaveAndPublishAsync(node);
        Logger.LogInformation("Published pillar O mnie.");
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
}
