using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;

namespace Storeasy.Application.Features.Apps.GetAppDetail;

/// <summary>
/// Validations of <see cref="GetAppDetailQuery"/>.
/// </summary>
public sealed class GetAppDetailQueryValidator : AbstractValidator<GetAppDetailQuery>
{
    ///<inheritdoc cref="GetAppDetailQueryValidator"/>
    public GetAppDetailQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(query => query.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
