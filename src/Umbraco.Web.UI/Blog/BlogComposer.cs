using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Cms.Web.UI.Blog;

public class BlogComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, BlogContentSeeder>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, CategoryRouteInitializer>();
        builder.AddNotificationHandler<ContentSavingNotification, CategorySlugSavingHandler>();
    }
}
