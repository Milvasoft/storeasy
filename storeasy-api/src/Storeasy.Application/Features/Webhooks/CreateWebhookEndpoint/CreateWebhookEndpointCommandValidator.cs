using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Events;

namespace Storeasy.Application.Features.Webhooks.CreateWebhookEndpoint;

/// <summary>
/// Validations of <see cref="CreateWebhookEndpointCommand"/>.
/// </summary>
public sealed class CreateWebhookEndpointCommandValidator : AbstractValidator<CreateWebhookEndpointCommand>
{
    ///<inheritdoc cref="CreateWebhookEndpointCommandValidator"/>
    public CreateWebhookEndpointCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Source)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalWebhookEndpoint]]);

        RuleFor(command => command.StoreConnectionId)
            .NotNull()
            .GreaterThan(0)
            .When(command => command.Source == WebhookSource.AppStoreConnect)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreConnection]]);

        RuleFor(command => command.PushServiceAccount)
            .NotEmpty()
            .MaximumLength(WebhookEndpoint.PushServiceAccountMaxLength)
            .EmailAddress()
            .When(command => command.Source == WebhookSource.GooglePlay)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalServiceAccount]]);

        RuleFor(command => command.Name)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);
    }
}
