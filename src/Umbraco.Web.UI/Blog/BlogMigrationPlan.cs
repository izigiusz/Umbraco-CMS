using Umbraco.Cms.Core.Packaging;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Runs once on boot. Umbraco's default <c>PackageMigrationsUnattended</c> is true,
/// which is not an unattended install and does not recreate the database.
/// </summary>
public class BlogMigrationPlan : PackageMigrationPlan
{
    public BlogMigrationPlan()
        : base("Umbraco.Web.UI.Blog")
    {
    }

    protected override void DefinePlan()
    {
        To<CreateBlogSchemaMigration>("1.0.0");
        To<AddFeaturedTopicsMigration>("1.1.0");
    }
}
