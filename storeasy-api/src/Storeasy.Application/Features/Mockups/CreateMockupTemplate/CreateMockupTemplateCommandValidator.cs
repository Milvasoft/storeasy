using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.CreateMockupTemplate;

/// <summary>
/// Validations of <see cref="CreateMockupTemplateCommand"/>.
/// </summary>
public sealed class CreateMockupTemplateCommandValidator : AbstractValidator<CreateMockupTemplateCommand>
{
    ///<inheritdoc cref="CreateMockupTemplateCommandValidator"/>
    public CreateMockupTemplateCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        this.RuleForMockupName(command => command.Name, localizer);
        this.RuleForMockupLocales(command => command.Locales, command => command.PrimaryLocale, localizer);

        RuleFor(command => command.SlotCount)
            .InclusiveBetween(1, MockupTemplate.MaxSlotCount)
            .WithMessage(localizer[MessageKey.MockupSlotInvalid])
            .When(command => command.SlotCount != null);

        RuleFor(command => command.CopyFromTemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]])
            .When(command => command.CopyFromTemplateId != null);
    }
}
