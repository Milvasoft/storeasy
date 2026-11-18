using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Artifacts.OpenArtifactSession;

/// <summary>
/// Validations of <see cref="OpenArtifactSessionCommand"/>.
/// </summary>
public sealed class OpenArtifactSessionCommandValidator : AbstractValidator<OpenArtifactSessionCommand>
{
    ///<inheritdoc cref="OpenArtifactSessionCommandValidator"/>
    public OpenArtifactSessionCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.FileName)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Length <= ArtifactSession.FileNameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalFileName]]);

        RuleFor(x => x.FileName)
            .Must(name => IntakeRules.Classify(name) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.FileName))
            .WithMessage(localizer[MessageKey.ArtifactTypeNotSupported]);

        RuleFor(x => x.Size)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalFileSize]]);

        RuleFor(x => x.Sha256)
            .Must(IntakeRules.IsSha256)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalSha256]]);

        RuleFor(x => x.Delivery.Notes)
            .MaximumLength(IntakeRules.NotesMaxLength)
            .When(x => x.Delivery != null)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalReleaseNotes]]);
    }
}
