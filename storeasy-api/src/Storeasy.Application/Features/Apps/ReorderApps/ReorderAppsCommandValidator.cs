using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.ReorderApps;

/// <summary>
/// Validations of <see cref="ReorderAppsCommand"/>.
/// </summary>
public sealed class ReorderAppsCommandValidator : AbstractValidator<ReorderAppsCommand>
{
    ///<inheritdoc cref="ReorderAppsCommandValidator"/>
    public ReorderAppsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.AppIds)
            .Must(ids => ids != null
                         && ids.Count > 0
                         && ids.Count <= ReorderAppsCommand.MaxAppCount
                         && ids.TrueForAll(id => id > 0)
                         && ids.Distinct().Count() == ids.Count)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
