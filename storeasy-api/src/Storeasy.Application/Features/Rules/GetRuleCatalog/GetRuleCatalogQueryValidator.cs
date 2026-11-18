using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.Rules.GetRuleCatalog;

/// <summary>
/// Validations of <see cref="GetRuleCatalogQuery"/>.
/// </summary>
public sealed class GetRuleCatalogQueryValidator : AbstractValidator<GetRuleCatalogQuery>
{
    ///<inheritdoc cref="GetRuleCatalogQueryValidator"/>
    public GetRuleCatalogQueryValidator(IMilvaLocalizer localizer)
    {
    }
}
