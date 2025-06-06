using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Cms.Core.Webhooks.Events;

[WebhookEvent("Partial View Deleted")]
public class PartialViewDeletedWebhookEvent : WebhookEventBase<PartialViewDeletedNotification>
{
    public PartialViewDeletedWebhookEvent(
        IWebhookFiringService webhookFiringService,
        IWebhookService webHookService,
        IOptionsMonitor<WebhookSettings> webhookSettings,
        IServerRoleAccessor serverRoleAccessor)
        : base(webhookFiringService, webHookService, webhookSettings, serverRoleAccessor)
    {
    }

    public override string Alias => Constants.WebhookEvents.Aliases.PartialViewDeleted;

    public override object? ConvertNotificationToRequestPayload(PartialViewDeletedNotification notification)
        => notification.DeletedEntities.Select(entity => new DefaultPayloadModel { Id = entity.Key });
}
