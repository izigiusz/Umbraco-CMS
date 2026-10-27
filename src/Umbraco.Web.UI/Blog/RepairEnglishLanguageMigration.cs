using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Repeats <see cref="AddEnglishLanguageMigration" />.
/// 1.6.0 can be recorded as finished while /en/ still 404s: a failed publish rolls the
/// transaction back, but a later boot can also commit languages without a routable English culture.
/// This step is safe to run again. Existing Polish and English values are left in place.
/// </summary>
public class RepairEnglishLanguageMigration : AddEnglishLanguageMigration
{
    public RepairEnglishLanguageMigration(
        IMigrationContext context,
        ILanguageService languageService,
        IDictionaryItemService dictionaryItemService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IContentPublishingService publishingService,
        IDomainService domainService,
        AppCaches appCaches)
        : base(
            context,
            languageService,
            dictionaryItemService,
            contentTypeService,
            contentService,
            publishingService,
            domainService,
            appCaches)
    {
    }
}
