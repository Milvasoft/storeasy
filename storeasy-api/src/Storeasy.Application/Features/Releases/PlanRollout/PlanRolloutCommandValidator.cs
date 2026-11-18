using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.PlanRollout;

/// <summary>
/// Validations of <see cref="PlanRolloutCommand"/>.
/// </summary>
public sealed class PlanRolloutCommandValidator : AbstractValidator<PlanRolloutCommand>
{
    ///<inheritdoc cref="PlanRolloutCommandValidator"/>
    public PlanRolloutCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Action)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.RolloutInvalid]);

        RuleFor(x => x.Percentage)
            .NotNull()
            .ExclusiveBetween(0, 100)
            .When(x => x.Action == RolloutAction.SetPercentage)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPercentage]]);
    }
}
