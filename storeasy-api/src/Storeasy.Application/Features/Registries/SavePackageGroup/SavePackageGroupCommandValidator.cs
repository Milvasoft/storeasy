using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.SavePackageGroup;

/// <summary>
/// Validations of <see cref="SavePackageGroupCommand"/>.
/// </summary>
public sealed class SavePackageGroupCommandValidator : AbstractValidator<SavePackageGroupCommand>
{
    ///<inheritdoc cref="SavePackageGroupCommandValidator"/>
    public SavePackageGroupCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]])
            .MaximumLength(PackageGroup.NameMaxLength)
            .WithMessage(localizer[MessageKey.MaxLengthReached, localizer[MessageKey.GlobalName], PackageGroup.NameMaxLength]);

        RuleFor(command => command.TrackedPackageIds)
            .NotNull()
            .Must(ids => ids.Count <= SavePackageGroupCommand.MaxMembers && ids.All(id => id > 0))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrackedPackage]]);

        RuleFor(command => command.Id)
            .GreaterThan(0)
            .When(command => command.Id.HasValue)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPackageGroup]]);
    }
}
