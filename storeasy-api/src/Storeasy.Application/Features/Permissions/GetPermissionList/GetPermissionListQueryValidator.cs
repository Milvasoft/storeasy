using FluentValidation;

namespace Storeasy.Application.Features.Permissions.GetPermissionList;

/// <summary>
/// Validations of <see cref="GetPermissionListQuery"/>.
/// </summary>
public sealed class GetPermissionListQueryValidator : AbstractValidator<GetPermissionListQuery>
{
    ///<inheritdoc cref="GetPermissionListQueryValidator"/>
    public GetPermissionListQueryValidator()
    {
    }
}