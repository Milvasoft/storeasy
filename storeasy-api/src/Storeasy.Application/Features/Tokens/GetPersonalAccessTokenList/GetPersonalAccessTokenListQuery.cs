using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.TokenDtos;

namespace Storeasy.Application.Features.Tokens.GetPersonalAccessTokenList;

/// <summary>
/// Lists the caller's own personal access tokens of every organization (design 16.11): active ones and those that
/// expired in the last 30 days. Revoked tokens are not listed.
/// </summary>
public record GetPersonalAccessTokenListQuery : IQuery<List<PersonalAccessTokenDto>>
{
}
