using Microsoft.AspNetCore.Http;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Reads the storeasy token of the request from the claims the token authentication set (no database).
/// </summary>
/// <param name="httpContextAccessor"></param>
public class CurrentToken(IHttpContextAccessor httpContextAccessor) : ICurrentToken
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc/>
    public TokenKind? Kind => Enum.TryParse<TokenKind>(Claim(TokenClaimNames.Kind), out var kind) ? kind : null;

    /// <inheritdoc/>
    public long? TokenId => long.TryParse(Claim(TokenClaimNames.TokenId), out var id) ? id : null;

    /// <inheritdoc/>
    public int? OrganizationId => int.TryParse(Claim(TokenClaimNames.OrganizationId), out var id) ? id : null;

    /// <inheritdoc/>
    public bool ReadOnly => Claim(TokenClaimNames.ReadOnly) == bool.TrueString;

    /// <inheritdoc/>
    public bool AllApps => Claim(TokenClaimNames.AllApps) == bool.TrueString;

    /// <inheritdoc/>
    public IReadOnlyList<int> AppIds => (Claim(TokenClaimNames.AppIds) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                                                         .Select(v => int.TryParse(v, out var id) ? id : 0)
                                                                                         .Where(id => id > 0)
                                                                                         .ToList();

    /// <inheritdoc/>
    public string Prefix => Claim(TokenClaimNames.Prefix);

    /// <inheritdoc/>
    public string Name => Claim(TokenClaimNames.Name);

    /// <inheritdoc/>
    public int? TrustedPublisherId => int.TryParse(Claim(TokenClaimNames.TrustedPublisherId), out var id) ? id : null;

    private string Claim(string type) => _httpContextAccessor.HttpContext?.User?.FindFirst(type)?.Value;
}
