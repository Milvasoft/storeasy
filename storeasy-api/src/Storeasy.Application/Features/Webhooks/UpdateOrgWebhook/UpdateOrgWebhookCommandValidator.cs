using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Webhooks.UpdateOrgWebhook;

/// <summary>
/// Validations of <see cref="UpdateOrgWebhookCommand"/>.
/// </summary>
public sealed class UpdateOrgWebhookCommandValidator : AbstractValidator<UpdateOrgWebhookCommand>
{
    ///<inheritdoc cref="UpdateOrgWebhookCommandValidator"/>
    public UpdateOrgWebhookCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .MaximumLength(OrganizationWebhook.NameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(command => command.Url)
            .MaximumLength(OrganizationWebhook.UrlMaxLength)
            .WithMessage(localizer[MessageKey.OrgWebhookUrlInvalid]);

        RuleForEach(command => command.Events)
            .Must(eventType => OrgWebhookEventTypes.All.Contains(eventType))
            .WithMessage(localizer[MessageKey.OrgWebhookEventInvalid]);
    }
}
