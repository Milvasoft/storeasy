using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.Rules.GetRuleDetail;

/// <summary>
/// Validations of <see cref="GetRuleDetailQuery"/>.
/// </summary>
public sealed class GetRuleDetailQueryValidator : AbstractValidator<GetRuleDetailQuery>
{
    ///<inheritdoc cref="GetRuleDetailQueryValidator"/>
    public GetRuleDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.RuleId)
            .NotEmpty()
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalRule]]);
    }
}
