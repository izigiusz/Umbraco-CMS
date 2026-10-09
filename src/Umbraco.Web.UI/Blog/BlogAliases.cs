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

    public const string HomeTemplate = "Home";
    public const string BlogTemplate = "Blog";
    public const string PostTemplate = "BlogPost";

    public const string Intro = "intro";
    public const string PublishDate = "publishDate";
    public const string Summary = "summary";
    public const string Body = "body";
    public const string CoverImage = "coverImage";
    public const string Tags = "tags";

    public const string PropertyGroup = "content";
}
