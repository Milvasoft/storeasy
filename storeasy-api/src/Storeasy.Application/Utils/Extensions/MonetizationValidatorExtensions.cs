using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Validation rules shared by the monetization planning requests.
/// </summary>
public static class MonetizationValidatorExtensions
{
    /// <summary>
    /// The store record must be sent and the changes must be a non empty list of known kinds.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="validator"></param>
    /// <param name="localizer"></param>
    public static void PlanChangesRules<T>(this AbstractValidator<T> validator, IMilvaLocalizer localizer) where T : IMonetizationPlanRequest
    {
        validator.RuleFor(x => x.StoreAppId)
                 .GreaterThan(0)
                 .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        validator.RuleFor(x => x.Changes)
                 .NotEmpty()
                 .WithMessage(localizer[MessageKey.PlanNoChanges]);

        validator.RuleForEach(x => x.Changes)
                 .Must(c => c != null && Enum.IsDefined(c.Kind))
                 .WithMessage(localizer[MessageKey.PleaseSendCorrect, nameof(MonetizationChangeDto.Kind)]);
    }
}
