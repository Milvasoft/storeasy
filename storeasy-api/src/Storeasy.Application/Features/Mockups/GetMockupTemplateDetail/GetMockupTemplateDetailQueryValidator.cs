using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Mockups.GetMockupTemplateDetail;

/// <summary>
/// Validations of <see cref="GetMockupTemplateDetailQuery"/>.
/// </summary>
public sealed class GetMockupTemplateDetailQueryValidator : AbstractValidator<GetMockupTemplateDetailQuery>
{
    ///<inheritdoc cref="GetMockupTemplateDetailQueryValidator"/>
    public GetMockupTemplateDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.TemplateId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalMockupTemplate]]);
    }
}
