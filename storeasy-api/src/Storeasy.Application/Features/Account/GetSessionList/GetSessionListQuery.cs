using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.GetSessionList;

/// <summary>
/// Lists the caller's active sessions (signed in devices), the current one first, then the most recently seen.
/// </summary>
public record GetSessionListQuery : IQuery<List<AccountSessionDto>>
{
}
