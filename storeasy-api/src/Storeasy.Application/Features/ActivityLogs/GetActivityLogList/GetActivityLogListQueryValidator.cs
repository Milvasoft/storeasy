using FluentValidation;

namespace Storeasy.Application.Features.ActivityLogs.GetActivityLogList;

/// <summary>
/// Validations of <see cref="GetActivityLogListQuery"/>.
/// </summary>
public sealed class GetActivityLogListQueryValidator : AbstractValidator<GetActivityLogListQuery>
{
    ///<inheritdoc cref="GetActivityLogListQueryValidator"/>
    public GetActivityLogListQueryValidator()
    {
    }
}