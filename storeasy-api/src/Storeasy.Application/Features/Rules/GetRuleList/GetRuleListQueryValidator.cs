using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.Rules.GetRuleList;

/// <summary>
/// Validations of <see cref="GetRuleListQuery"/>.
/// </summary>
public sealed class GetRuleListQueryValidator : AbstractValidator<GetRuleListQuery>
{
    ///<inheritdoc cref="GetRuleListQueryValidator"/>
    public GetRuleListQueryValidator(IMilvaLocalizer localizer)
    {
    }
}
