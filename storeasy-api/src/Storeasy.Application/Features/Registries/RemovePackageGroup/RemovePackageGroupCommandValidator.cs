using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Registries.RemovePackageGroup;

/// <summary>
/// Validations of <see cref="RemovePackageGroupCommand"/>.
/// </summary>
public sealed class RemovePackageGroupCommandValidator : AbstractValidator<RemovePackageGroupCommand>
{
    ///<inheritdoc cref="RemovePackageGroupCommandValidator"/>
    public RemovePackageGroupCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Id)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPackageGroup]]);
    }
}
