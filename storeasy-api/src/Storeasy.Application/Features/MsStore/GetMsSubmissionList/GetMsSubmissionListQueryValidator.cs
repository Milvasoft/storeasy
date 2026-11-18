using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.MsStore.GetMsSubmissionList;

/// <summary>
/// Validations of <see cref="GetMsSubmissionListQuery"/>.
/// </summary>
public sealed class GetMsSubmissionListQueryValidator : AbstractValidator<GetMsSubmissionListQuery>
{
    ///<inheritdoc cref="GetMsSubmissionListQueryValidator"/>
    public GetMsSubmissionListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
