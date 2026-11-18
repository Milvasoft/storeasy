using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.ArtifactSources.SaveArtifactSource;

/// <summary>
/// Validations of <see cref="SaveArtifactSourceCommand"/>.
/// </summary>
public sealed class SaveArtifactSourceCommandValidator : AbstractValidator<SaveArtifactSourceCommand>
{
    ///<inheritdoc cref="SaveArtifactSourceCommandValidator"/>
    public SaveArtifactSourceCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .When(x => x.Id == null)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[nameof(ArtifactPullSourceKind)]]);

        RuleFor(x => x.AllowedDomains)
            .Must(domains => domains != null && domains.Count > 0 && domains.Count <= ArtifactPullSource.MaxDomains && domains.TrueForAll(IntakeRules.IsValidDomainEntry))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalDomain]]);

        RuleFor(x => x.Platforms)
            .Must(platforms => platforms != null && platforms.Count > 0 && platforms.TrueForAll(p => p is ArtifactPlatform.Ios or ArtifactPlatform.Android or ArtifactPlatform.Windows))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalPlatform]]);
    }
}
