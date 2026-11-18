using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroupBuilds;

/// <summary>
/// Validations of <see cref="UpdateBetaGroupBuildsCommand"/>.
/// </summary>
public sealed class UpdateBetaGroupBuildsCommandValidator : AbstractValidator<UpdateBetaGroupBuildsCommand>
{
    ///<inheritdoc cref="UpdateBetaGroupBuildsCommandValidator"/>
    public UpdateBetaGroupBuildsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command)
            .Must(command => (command.AddBuildIds?.Count ?? 0) + (command.RemoveBuildIds?.Count ?? 0) is > 0 and <= 50)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalBuild]]);
    }
}
