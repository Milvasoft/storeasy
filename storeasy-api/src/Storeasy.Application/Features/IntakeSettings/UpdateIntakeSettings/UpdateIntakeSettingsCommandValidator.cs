using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.IntakeSettings.UpdateIntakeSettings;

/// <summary>
/// Validations of <see cref="UpdateIntakeSettingsCommand"/>.
/// </summary>
public sealed class UpdateIntakeSettingsCommandValidator : AbstractValidator<UpdateIntakeSettingsCommand>
{
    ///<inheritdoc cref="UpdateIntakeSettingsCommandValidator"/>
    public UpdateIntakeSettingsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.ArtifactRetentionDays)
            .InclusiveBetween(1, 365)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRetention]]);

        RuleFor(x => x.MaxArtifactBytes)
            .InclusiveBetween(IntakeRules.MinPartSize, IntakeSetting.DefaultMaxArtifactBytes)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalFileSize]]);
    }
}
