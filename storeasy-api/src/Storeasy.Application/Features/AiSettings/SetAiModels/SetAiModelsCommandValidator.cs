using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Application.Features.AiSettings.SetAiModels;

/// <summary>
/// Validations of <see cref="SetAiModelsCommand"/>: only models of <see cref="AiModelCatalog"/>.
/// </summary>
public sealed class SetAiModelsCommandValidator : AbstractValidator<SetAiModelsCommand>
{
    ///<inheritdoc cref="SetAiModelsCommandValidator"/>
    public SetAiModelsCommandValidator(IMilvaLocalizer localizer)
    {
        RuleFor(command => command.ReplyDraftModel)
            .Must(model => model == null || AiModelCatalog.IsKnown(model))
            .WithMessage(localizer[MessageKey.AiModelInvalid]);

        RuleFor(command => command.PreReviewModel)
            .Must(model => model == null || AiModelCatalog.IsKnown(model))
            .WithMessage(localizer[MessageKey.AiModelInvalid]);
    }
}
