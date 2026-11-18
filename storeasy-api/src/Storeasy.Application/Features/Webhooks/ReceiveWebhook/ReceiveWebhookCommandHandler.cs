using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Storeasy.Application.Features.Webhooks.ReceiveWebhook;

/// <summary>
/// Handles <see cref="ReceiveWebhookCommand"/>. A repeated event answers success, so the sender stops retrying it.
/// </summary>
/// <param name="WebhookInbox"></param>
public record ReceiveWebhookCommandHandler(IWebhookInbox WebhookInbox) : IInterceptable, ICommandHandler<ReceiveWebhookCommand, long>
{
    private readonly IWebhookInbox _webhookInbox = WebhookInbox;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(ReceiveWebhookCommand request, CancellationToken cancellationToken)
    {
        var receipt = await _webhookInbox.ReceiveAsync(new InboundWebhookRequest(request.Source, request.PublicId, request.Body ?? string.Empty, request.Signature, request.Timestamp, request.EventId), cancellationToken);

        return receipt.Outcome switch
        {
            WebhookReceiptOutcome.Accepted or WebhookReceiptOutcome.Duplicate => Response<long>.Success(receipt.EventId ?? 0),
            WebhookReceiptOutcome.NoEndpoint => Response<long>.Error(0, MessageKey.WebhookEndpointNotFound),
            WebhookReceiptOutcome.InvalidSignature => Response<long>.Error(0, MessageKey.WebhookSignatureInvalid),
            _ => Response<long>.Error(0, MessageKey.WebhookEventStale),
        };
    }
}
