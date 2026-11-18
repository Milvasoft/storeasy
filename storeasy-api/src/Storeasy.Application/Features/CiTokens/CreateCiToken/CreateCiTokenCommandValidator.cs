using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.CiTokens.CreateCiToken;

/// <summary>
/// Validations of <see cref="CreateCiTokenCommand"/>.
/// </summary>
public sealed class CreateCiTokenCommandValidator : AbstractValidator<CreateCiTokenCommand>
{
    ///<inheritdoc cref="CreateCiTokenCommandValidator"/>
    public CreateCiTokenCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= CiToken.NameMaxLength)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalName]]);

        RuleFor(x => x.ExpiryDays)
            .InclusiveBetween(1, CiToken.MaxExpiryDays)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalExpiryDays]]);

        RuleFor(x => x.AppIds)
            .Must((command, appIds) => command.AllApps || appIds?.Count > 0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.Scopes)
            .Must(scopes => scopes == null || scopes.TrueForAll(s => s == CiToken.ArtifactsWriteScope))
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalScope]]);
    }
}
