using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanListing;

/// <summary>
/// Validations of <see cref="PlanListingCommand"/>.
/// </summary>
public sealed class PlanListingCommandValidator : AbstractValidator<PlanListingCommand>
{
    ///<inheritdoc cref="PlanListingCommandValidator"/>
    public PlanListingCommandValidator(IMilvaLocalizer localizer, IRulesCatalog rulesCatalog)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);

        RuleFor(x => x.Changes)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.CannotBeEmpty, localizer[MessageKey.GlobalField]]);

        RuleForEach(x => x.Changes).Custom((change, context) =>
        {
            var field = Storeasy.Domain.Content.ListingFields.All.FirstOrDefault(f => string.Equals(f.Field, change.Field, StringComparison.OrdinalIgnoreCase));
            var name = $"Fields[{change.Locale}].{change.Field}";

            if (field == null)
            {
                context.AddFailure(name, localizer[MessageKey.FieldUnknown, change.Field]);
                return;
            }

            if (field.Localized && string.IsNullOrWhiteSpace(change.Locale))
            {
                context.AddFailure(name, localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalLocale]]);
                return;
            }

            if (FieldPlanner.LimitViolation(field, change.Value, rulesCatalog.Current) is { } violation)
                context.AddFailure(name, localizer[violation.Key, field.Field, violation.Actual, violation.Limit]);

            if (field.IsUrl && !string.IsNullOrEmpty(change.Value) && !Uri.TryCreate(change.Value, UriKind.Absolute, out _))
                context.AddFailure(name, localizer[MessageKey.InvalidUrlAddress]);
        });
    }
}
