using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.StoreJobs.GetStoreJobList;

/// <summary>
/// Validations of <see cref="GetStoreJobListQuery"/>.
/// </summary>
public sealed class GetStoreJobListQueryValidator : AbstractValidator<GetStoreJobListQuery>
{
    ///<inheritdoc cref="GetStoreJobListQueryValidator"/>
    public GetStoreJobListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]])
            .When(query => query.AppId.HasValue);
    }
}
