using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Extensions;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Publishes a Home page, a Blog page, and one example post when the content tree is empty.
/// Runs at the end of boot, after package migrations and after core components are initialized,
/// and before the host starts listening. A later boot does nothing once any root content exists.
/// </summary>
public class BlogContentSeeder : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentPublishingService _publishingService;
    private readonly ILanguageService _languageService;
    private readonly ILogger<BlogContentSeeder> _logger;

    public BlogContentSeeder(
        IContentService contentService,
        IContentTypeService contentTypeService,
        IContentPublishingService publishingService,
        ILanguageService languageService,
        ILogger<BlogContentSeeder> logger)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _publishingService = publishingService;
        _languageService = languageService;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartingNotification notification, CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
        {
            return;
        }

        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Blog sample content was not created. The site will continue with whatever content is already there.");
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_contentService.GetRootContent().Any())
        {
            _logger.LogInformation("Content already exists. Blog sample content was not changed.");
            return;
        }

        IContentType? homeType = _contentTypeService.Get(BlogAliases.Home);
        IContentType? blogType = _contentTypeService.Get(BlogAliases.Blog);
        IContentType? postType = _contentTypeService.Get(BlogAliases.Post);
        if (homeType is null || blogType is null || postType is null || postType.PropertyTypeExists(BlogAliases.Body) is false)
        {
            _logger.LogInformation("Blog document types are not available yet, so no sample content was created.");
            return;
        }

        var created = new List<IContent>();
        try
        {
            ILanguage? language = await _languageService.GetDefaultLanguageAsync();
            var culture = language?.IsoCode;

            IContent home = Create(homeType, "Home", Constants.System.Root, culture);
            SetText(home, BlogAliases.Intro, "A small blog. New posts appear here and on the Blog page.", culture);
            await SaveAndPublishAsync(home, created, culture);

            IContent blog = Create(blogType, "Blog", home.Id, culture);
            SetText(blog, BlogAliases.Intro, "Newest posts first.", culture);
            await SaveAndPublishAsync(blog, created, culture);

            IContent post = Create(postType, "Hello from the blog", blog.Id, culture);
            SetText(post, BlogAliases.PublishDate, DateTime.Now, culture);
            SetText(post, BlogAliases.Summary, "A short example post so this site has published content after the first deploy.", culture);
            SetText(
                post,
                BlogAliases.Body,
                "<p>This post was created automatically. Edit it, add a cover image, or delete it from the backoffice.</p><p>Further posts go under Blog and show up here, newest first.</p>",
                culture);
            SetText(post, BlogAliases.Tags, "[\"umbraco\",\"blog\"]", culture);
            await SaveAndPublishAsync(post, created, culture);

            _logger.LogInformation("Published sample Home, Blog, and one blog post.");
        }
        catch
        {
            foreach (IContent content in created.AsEnumerable().Reverse())
            {
                try
                {
                    _contentService.Delete(content);
                }
                catch (Exception deleteException)
                {
                    _logger.LogWarning(deleteException, "Could not roll back sample content {Name}.", content.Name);
                }
            }

            throw;
        }
    }

    private IContent Create(IContentType contentType, string name, int parentId, string? culture)
    {
        IContent content = _contentService.Create(name, parentId, contentType);
        if (content.ContentType.VariesByCulture() && string.IsNullOrWhiteSpace(culture) is false)
        {
            content.SetCultureName(name, culture);
        }

        if (contentType.DefaultTemplateId > 0)
        {
            content.TemplateId = contentType.DefaultTemplateId;
        }

        return content;
    }

    private static void SetText(IContent content, string alias, object? value, string? culture)
    {
        var propertyCulture = content.Properties[alias]?.PropertyType.VariesByCulture() == true ? culture : null;
        content.SetValue(alias, value, propertyCulture);
    }

    private async Task SaveAndPublishAsync(IContent content, List<IContent> created, string? culture)
    {
        OperationResult saved = _contentService.Save(content);
        if (saved.Success is false)
        {
            throw new InvalidOperationException($"Saving '{content.Name}' failed: {saved.Result}.");
        }

        // Track the row as soon as it is stored, so a failed publish can delete it
        // and the next boot can try the empty tree again.
        created.Add(content);

        var publishCulture = content.ContentType.VariesByCulture() ? culture : null;
        Attempt<ContentPublishingResult, ContentPublishingOperationStatus> published = await _publishingService.PublishAsync(
            content.Key,
            [new CulturePublishScheduleModel { Culture = publishCulture }],
            Constants.Security.SuperUserKey);

        if (published.Success is false)
        {
            throw new InvalidOperationException($"Publishing '{content.Name}' failed: {published.Status}.");
        }
    }
}
