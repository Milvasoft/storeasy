using Milvasoft.Caching.Redis.Accessor;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using System.Globalization;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IOrganizationAccess"/>. The role (and, for a custom role, its permissions) is read from
/// <c>OrganizationMembers</c> and cached in Redis for <see cref="CacheExpiry"/>; membership and custom role changes call
/// <see cref="InvalidateAsync"/> or <see cref="InvalidateManyAsync"/>, so a removed member loses access on the next request.
/// While Redis is unavailable every check reads the database (never fails open).
/// </summary>
/// <param name="cache"></param>
/// <param name="memberRepository"></param>
/// <param name="logger"></param>
/// <param name="cacheKeys"></param>
public class OrganizationAccess(IRedisAccessor cache, IStoreasyRepositoryBase<OrganizationMember> memberRepository, IMilvaLogger logger, CacheKeys cacheKeys) : IOrganizationAccess
{
    /// <summary>How long an access is served from the cache.</summary>
    public static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(60);

    // Cached value for "not a member", so repeated probes of a foreign organization do not hit the database.
    private const string _noRole = "0";

    private readonly IRedisAccessor _cache = cache;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = memberRepository;
    private readonly IMilvaLogger _logger = logger;
    private readonly CacheKeys _cacheKeys = cacheKeys;

    /// <inheritdoc/>
    public OrgAccess Current { get; private set; }

    /// <inheritdoc/>
    public void SetCurrent(OrgAccess access) => Current = access;

    /// <inheritdoc/>
    public async Task<OrgAccess> GetAccessAsync(int organizationId, int userId, CancellationToken cancellationToken)
    {
        var key = _cacheKeys.OrganizationRole(organizationId, userId);
        var cached = await TryGetCacheAsync(key);

        if (cached != null)
            return Parse(cached);

        var member = await _memberRepository.GetFirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId && !m.Organization.IsDeleted,
                                                                    projection: m => new { m.Role, m.CustomRoleId, Permissions = m.CustomRole != null ? m.CustomRole.Permissions : null },
                                                                    cancellationToken: cancellationToken);

        var access = member == null ? null : OrgAccess.Of(member.Role, member.CustomRoleId, member.Permissions);

        await TrySetCacheAsync(key, Format(access));

        return access;
    }

    /// <inheritdoc/>
    public Task InvalidateAsync(int organizationId, int userId) => InvalidateManyAsync(organizationId, [userId]);

    /// <inheritdoc/>
    public async Task InvalidateManyAsync(int organizationId, IReadOnlyCollection<int> userIds)
    {
        if (userIds.Count == 0 || !_cache.IsConnected())
            return;

        try
        {
            await _cache.GetDatabase().KeyDeleteAsync([.. userIds.Select(userId => (StackExchange.Redis.RedisKey)_cacheKeys.OrganizationRole(organizationId, userId))]);
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Organization access cache entries of {OrganizationId} could not be removed.", organizationId);
        }
    }

    /// <summary>
    /// The cached form: "0" for no membership, the role number for a seeded role, "5:{customRoleId}:{permissions}" for a
    /// custom role.
    /// </summary>
    private static string Format(OrgAccess access)
    {
        if (access == null)
            return _noRole;

        var role = ((byte)access.Role).ToString(CultureInfo.InvariantCulture);

        return access.CustomRoleId.HasValue ? $"{role}:{access.CustomRoleId.Value.ToString(CultureInfo.InvariantCulture)}:{string.Join(',', access.Permissions.Select(p => (byte)p))}" : role;
    }

    private static OrgAccess Parse(string value)
    {
        var parts = value.Split(':');

        if (!byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var role) || role == 0)
            return null;

        if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var customRoleId))
        {
            var permissions = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(p => byte.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? (OrgPermission)number : OrgPermission.View);

            return OrgAccess.Custom(customRoleId, permissions);
        }

        return OrgAccess.Of((OrgRole)role, null, null);
    }

    private async Task<string> TryGetCacheAsync(string key)
    {
        if (!_cache.IsConnected())
            return null;

        try
        {
            return await _cache.GetAsync<string>(key);
        }
        catch
        {
            return null;
        }
    }

    private async Task TrySetCacheAsync(string key, string value)
    {
        if (!_cache.IsConnected())
            return;

        try
        {
            await _cache.SetAsync(key, value, CacheExpiry);
        }
        catch
        {
            // Redis unavailable: the database remains the source of truth; ignore.
        }
    }
}
