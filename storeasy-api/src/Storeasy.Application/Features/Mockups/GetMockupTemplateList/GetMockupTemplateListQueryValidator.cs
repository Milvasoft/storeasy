using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Mockups.GetMockupTemplateList;

/// <summary>
/// Validations of <see cref="GetMockupTemplateListQuery"/>.
/// </summary>
public sealed class GetMockupTemplateListQueryValidator : AbstractValidator<GetMockupTemplateListQuery>
{
    ///<inheritdoc cref="GetMockupTemplateListQueryValidator"/>
    public GetMockupTemplateListQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]])
            .When(query => query.AppId != null);
    }
}
