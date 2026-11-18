using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.StoreJobs.GetStoreJobDetail;

/// <summary>
/// Validations of <see cref="GetStoreJobDetailQuery"/>.
/// </summary>
public sealed class GetStoreJobDetailQueryValidator : AbstractValidator<GetStoreJobDetailQuery>
{
    ///<inheritdoc cref="GetStoreJobDetailQueryValidator"/>
    public GetStoreJobDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.JobId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreJob]]);
    }
}
