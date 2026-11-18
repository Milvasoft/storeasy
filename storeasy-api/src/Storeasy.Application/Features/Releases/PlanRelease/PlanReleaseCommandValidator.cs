using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Releases.PlanRelease;

/// <summary>
/// Validations of <see cref="PlanReleaseCommand"/>.
/// </summary>
public sealed class PlanReleaseCommandValidator : AbstractValidator<PlanReleaseCommand>
{
    ///<inheritdoc cref="PlanReleaseCommandValidator"/>
    public PlanReleaseCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlan]]);

        RuleFor(x => x.Track)
            .NotEmpty()
            .When(x => x.Kind == ReleasePlanKind.AssignTrack)
            .WithMessage(localizer[MessageKey.TrackInvalid]);

        RuleFor(x => x.VersionCodes)
            .NotEmpty()
            .When(x => x.Kind == ReleasePlanKind.AssignTrack)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalVersion]]);

        RuleFor(x => x.Status)
            .Must(s => s is "completed" or "draft" or "inProgress")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalTrack]]);

        RuleFor(x => x.UserFraction)
            .ExclusiveBetween(0, 1)
            .When(x => x.Status == "inProgress")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPercentage]]);

        RuleForEach(x => x.ReleaseNotes)
            .Must(n => n != null && !string.IsNullOrWhiteSpace(n.Language) && (n.Text ?? string.Empty).Length <= 500)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalReleaseNotes]]);
    }
}
