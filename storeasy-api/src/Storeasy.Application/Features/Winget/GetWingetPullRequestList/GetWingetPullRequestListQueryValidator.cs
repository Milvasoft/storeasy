using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Winget.GetWingetPullRequestList;

/// <summary>
/// Validations of <see cref="GetWingetPullRequestListQuery"/>.
/// </summary>
public sealed class GetWingetPullRequestListQueryValidator : AbstractValidator<GetWingetPullRequestListQuery>
{
    ///<inheritdoc cref="GetWingetPullRequestListQueryValidator"/>
    public GetWingetPullRequestListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
