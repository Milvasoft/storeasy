using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.UpdateAppProfile;

/// <summary>
/// Validations of <see cref="UpdateAppProfileCommand"/>.
/// </summary>
public sealed class UpdateAppProfileCommandValidator : AbstractValidator<UpdateAppProfileCommand>
{
    ///<inheritdoc cref="UpdateAppProfileCommandValidator"/>
    public UpdateAppProfileCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);

        RuleFor(x => x.AppType)
            .Must(t => t == null || t is "app" or "game")
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalCategory]]);

        RuleFor(x => x.TargetSdk)
            .InclusiveBetween(1, 100)
            .When(x => x.TargetSdk != null)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalVersion]]);
    }
}
