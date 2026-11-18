using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Readiness.GetAppsOverview;

/// <summary>
/// Validations of <see cref="GetAppsOverviewQuery"/>.
/// </summary>
public sealed class GetAppsOverviewQueryValidator : AbstractValidator<GetAppsOverviewQuery>
{
    ///<inheritdoc cref="GetAppsOverviewQueryValidator"/>
    public GetAppsOverviewQueryValidator(IMilvaLocalizer localizer)
    {
    }
}
