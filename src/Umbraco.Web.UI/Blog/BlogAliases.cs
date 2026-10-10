namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Aliases shared by the blog migration and the sample-content seeder.
/// Razor templates repeat the same strings; keep them in sync with this class.
/// </summary>
public static class BlogAliases
{
    public const string Home = "home";
    public const string Blog = "blog";
    public const string Post = "blogPost";
    public const string Category = "category";
    public const string Categories = "categories";

    public const string HomeTemplate = "Home";
    public const string BlogTemplate = "Blog";
    public const string PostTemplate = "BlogPost";

    public const string Intro = "intro";
    public const string PublishDate = "publishDate";
    public const string Summary = "summary";
    public const string Body = "body";
    public const string CoverImage = "coverImage";
    public const string Tags = "tags";
    public const string FeaturedTopics = "featuredTopics";
    public const string CategoryName = "categoryName";
    public const string Slug = "slug";
    public const string Description = "description";

    public const string PropertyGroup = "content";
    public const string FeaturedGroup = "featured";

    public const string FeaturedTopicsDataTypeName = "Featured blog posts";
    public const string CategoryPickerDataTypeName = "Blog post category";

    public const string CategoryTemplate = "Category";
    public const string CategoriesTemplate = "Categories";
}
