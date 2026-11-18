using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Readiness.GetOrganizationCompliance;

/// <summary>
/// Validations of <see cref="GetOrganizationComplianceQuery"/>.
/// </summary>
public sealed class GetOrganizationComplianceQueryValidator : AbstractValidator<GetOrganizationComplianceQuery>
{
    ///<inheritdoc cref="GetOrganizationComplianceQueryValidator"/>
    public GetOrganizationComplianceQueryValidator(IMilvaLocalizer localizer)
    {
    }
}
