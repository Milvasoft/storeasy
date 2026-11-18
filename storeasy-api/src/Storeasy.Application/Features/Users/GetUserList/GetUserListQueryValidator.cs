using FluentValidation;

namespace Storeasy.Application.Features.Users.GetUserList;

/// <summary>
/// Validations of <see cref="GetUserListQuery"/>.
/// </summary>
public sealed class GetUserListQueryValidator : AbstractValidator<GetUserListQuery>
{
    ///<inheritdoc cref="GetUserListQueryValidator"/>
    public GetUserListQueryValidator()
    {
    }
}