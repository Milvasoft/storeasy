using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.TokenDtos;

namespace Storeasy.Application.Features.Tokens.GetCurrentToken;

/// <summary>
/// Describes the token of the request (tools/CONTRACT.md 2.1): personal access, CI or upload token. A web session has no
/// such token (404).
/// </summary>
public record GetCurrentTokenQuery : IQuery<CurrentTokenDto>
{
}
