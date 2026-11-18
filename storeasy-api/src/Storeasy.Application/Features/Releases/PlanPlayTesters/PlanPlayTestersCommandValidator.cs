using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.PlanPlayTesters;

/// <summary>
/// Validations of <see cref="PlanPlayTestersCommand"/>.
/// </summary>
public sealed class PlanPlayTestersCommandValidator : AbstractValidator<PlanPlayTestersCommand>
{
    ///<inheritdoc cref="PlanPlayTestersCommandValidator"/>
    public PlanPlayTestersCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Track)
            .NotEmpty()
            .Must(t => !Storeasy.Domain.Content.ChangePlanRules.IsProduction(t))
            .WithMessage(localizer[MessageKey.TrackInvalid]);

        RuleForEach(x => x.GoogleGroups)
            .EmailAddress()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalGoogleGroup]]);
    }
}
