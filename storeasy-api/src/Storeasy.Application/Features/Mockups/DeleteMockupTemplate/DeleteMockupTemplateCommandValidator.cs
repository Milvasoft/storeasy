using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Mockups.DeleteMockupTemplate;

/// <summary>
/// Validations of <see cref="DeleteMockupTemplateCommand"/>.
/// </summary>
public sealed class DeleteMockupTemplateCommandValidator : AbstractValidator<DeleteMockupTemplateCommand>
{
    ///<inheritdoc cref="DeleteMockupTemplateCommandValidator"/>
    public DeleteMockupTemplateCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);
    }
}
