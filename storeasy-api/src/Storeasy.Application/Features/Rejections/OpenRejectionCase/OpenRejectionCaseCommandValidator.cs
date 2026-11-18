using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Rejections.OpenRejectionCase;

/// <summary>
/// Validations of <see cref="OpenRejectionCaseCommand"/>.
/// </summary>
public sealed class OpenRejectionCaseCommandValidator : AbstractValidator<OpenRejectionCaseCommand>
{
    ///<inheritdoc cref="OpenRejectionCaseCommandValidator"/>
    public OpenRejectionCaseCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(command => command.VersionString)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalVersion]]);

        RuleFor(command => command.BuildNumber)
            .MaximumLength(100)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBuild]]);

        RuleFor(command => command.Store)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.StoreInvalid]);

        RuleFor(command => command.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRejectionCase]]);
    }
}
