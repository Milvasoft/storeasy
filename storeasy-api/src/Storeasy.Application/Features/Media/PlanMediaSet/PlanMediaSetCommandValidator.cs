using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Media.PlanMediaSet;

/// <summary>
/// Validations of <see cref="PlanMediaSetCommand"/>.
/// </summary>
public sealed class PlanMediaSetCommandValidator : AbstractValidator<PlanMediaSetCommand>
{
    ///<inheritdoc cref="PlanMediaSetCommandValidator"/>
    public PlanMediaSetCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Locale)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalLocale]]);

        RuleFor(x => x.ClassKey)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalClassKey]]);

        RuleFor(x => x.AssetIds)
            .Must(a => a == null || a.Count <= 10)
            .WithMessage(localizer[MessageKey.MediaTooMany, 10]);
    }
}
