using FluentValidation;

namespace Storeasy.Application.Features.Roles.GetRoleList;

/// <summary>
/// Validations of <see cref="GetRoleListQuery"/>.
/// </summary>
public sealed class GetRoleListQueryValidator : AbstractValidator<GetRoleListQuery>
{
    ///<inheritdoc cref="GetRoleListQueryValidator"/>
    public GetRoleListQueryValidator()
    {
    }
}