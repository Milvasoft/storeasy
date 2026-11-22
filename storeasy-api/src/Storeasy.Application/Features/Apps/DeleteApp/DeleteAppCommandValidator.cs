using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.DeleteApp;

/// <summary>
/// Validations of <see cref="DeleteAppCommand"/>.
/// </summary>
public sealed class DeleteAppCommandValidator : AbstractValidator<DeleteAppCommand>
{
    ///<inheritdoc cref="DeleteAppCommandValidator"/>
    public DeleteAppCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
