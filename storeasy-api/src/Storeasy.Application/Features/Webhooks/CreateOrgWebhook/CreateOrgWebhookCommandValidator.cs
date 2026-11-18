using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Webhooks.CreateOrgWebhook;

/// <summary>
/// Validations of <see cref="CreateOrgWebhookCommand"/>.
/// </summary>
public sealed class CreateOrgWebhookCommandValidator : AbstractValidator<CreateOrgWebhookCommand>
{
    ///<inheritdoc cref="CreateOrgWebhookCommandValidator"/>
    public CreateOrgWebhookCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(OrganizationWebhook.NameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(command => command.Url)
            .NotEmpty()
            .MaximumLength(OrganizationWebhook.UrlMaxLength)
            .WithMessage(localizer[MessageKey.OrgWebhookUrlInvalid]);

        RuleForEach(command => command.Events)
            .Must(eventType => OrgWebhookEventTypes.All.Contains(eventType))
            .WithMessage(localizer[MessageKey.OrgWebhookEventInvalid]);
    }
}
