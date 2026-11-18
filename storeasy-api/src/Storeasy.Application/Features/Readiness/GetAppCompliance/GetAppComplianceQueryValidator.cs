using FluentValidation;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Readiness.GetAppCompliance;

/// <summary>
/// Validations of <see cref="GetAppComplianceQuery"/>.
/// </summary>
public sealed class GetAppComplianceQueryValidator : AbstractValidator<GetAppComplianceQuery>
{
    ///<inheritdoc cref="GetAppComplianceQueryValidator"/>
    public GetAppComplianceQueryValidator(IMilvaLocalizer localizer)
    {
        RuleFor(x => x.AppId)
            .GreaterThan(0)
            .WithMessage(localizer[MessageKey.PleaseSendCorrect, localizer[MessageKey.GlobalApp]]);
    }
}
