using Milvasoft.Caching.Redis.Accessor;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IOrganizationAccess"/>. The role is read from <c>OrganizationMembers</c> and cached in Redis for
/// <see cref="CacheExpiry"/>; membership changes call <see cref="InvalidateAsync"/>, so a removed member loses access
/// on the next request. While Redis is unavailable every check reads the database (never fails open).
/// </summary>
/// <param name="cache"></param>
/// <param name="memberRepository"></param>
/// <param name="logger"></param>
/// <param name="cacheKeys"></param>
public class OrganizationAccess(IRedisAccessor cache, IStoreasyRepositoryBase<OrganizationMember> memberRepository, IMilvaLogger logger, CacheKeys cacheKeys) : IOrganizationAccess
{
    /// <summary>How long a role is served from the cache.</summary>
    public static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(60);

    // Cached value for "not a member", so repeated probes of a foreign organization do not hit the database.
    private const byte _noRole = 0;

    private readonly IRedisAccessor _cache = cache;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = memberRepository;
    private readonly IMilvaLogger _logger = logger;
    private readonly CacheKeys _cacheKeys = cacheKeys;

    /// <inheritdoc/>
    public OrgRole? CurrentRole { get; private set; }

    /// <inheritdoc/>
    public void SetCurrentRole(OrgRole role) => CurrentRole = role;

    /// <inheritdoc/>
    public async Task<OrgRole?> GetRoleAsync(int organizationId, int userId, CancellationToken cancellationToken)
    {
        var key = _cacheKeys.OrganizationRole(organizationId, userId);
        var cached = await TryGetCacheAsync(key);

        if (cached.HasValue)
            return cached.Value == _noRole ? null : (OrgRole)cached.Value;

        var role = await _memberRepository.GetFirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId && !m.Organization.IsDeleted,
                                                                  projection: m => (OrgRole?)m.Role,
                                                                  cancellationToken: cancellationToken);

        await TrySetCacheAsync(key, role.HasValue ? (byte)role.Value : _noRole);

        return role;
    }

    /// <inheritdoc/>
    public async Task InvalidateAsync(int organizationId, int userId)
    {
        if (!_cache.IsConnected())
            return;

        try
        {
            await _cache.RemoveAsync(_cacheKeys.OrganizationRole(organizationId, userId));
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Organization role cache entry {OrganizationId}:{UserId} could not be removed.", organizationId, userId);
        }
    }

    private async Task<byte?> TryGetCacheAsync(string key)
    {
        if (!_cache.IsConnected())
            return null;

        try
        {
            var value = await _cache.GetAsync<string>(key);

            return byte.TryParse(value, out var role) ? role : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task TrySetCacheAsync(string key, byte value)
    {
        if (!_cache.IsConnected())
            return;

        try
        {
            await _cache.SetAsync(key, value.ToString(), CacheExpiry);
        }
        catch
        {
            // Redis unavailable: the database remains the source of truth; ignore.
        }
    }
}
