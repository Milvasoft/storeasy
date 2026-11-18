using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Releases.GetReleaseOverview;

/// <summary>
/// Validations of <see cref="GetReleaseOverviewQuery"/>.
/// </summary>
public sealed class GetReleaseOverviewQueryValidator : AbstractValidator<GetReleaseOverviewQuery>
{
    ///<inheritdoc cref="GetReleaseOverviewQueryValidator"/>
    public GetReleaseOverviewQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.StoreAppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalStoreApp]]);
    }
}
