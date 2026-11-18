using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Behaviours;

namespace Storeasy.Application.Features.Mockups.ExportMockups;

/// <summary>
/// Validations of <see cref="ExportMockupsCommand"/>.
/// </summary>
public sealed class ExportMockupsCommandValidator : AbstractValidator<ExportMockupsCommand>
{
    ///<inheritdoc cref="ExportMockupsCommandValidator"/>
    public ExportMockupsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);

        this.RuleForMockupExportSelection(command => command.Locales, command => command.ClassKeys, command => command.Targets, localizer);
    }
}
