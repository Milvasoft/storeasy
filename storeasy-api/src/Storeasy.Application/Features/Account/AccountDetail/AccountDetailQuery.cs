using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.AccountDetail;

/// <summary>
/// Data transfer object for account details. The account is always the authenticated caller's own (taken from the token).
/// </summary>
public record AccountDetailQuery : IQuery<AccountDetailDto>
{
}
