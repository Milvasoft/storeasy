using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Tokens;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// Default <see cref="IOidcExchangeService"/>. An unknown app and an app without rules answer the same 404, so apps cannot
/// be probed; a valid token that matches no rule is a 403 that does not say which claim failed (the claims of the attempt
/// are kept on the rules for the diagnosis drawer instead).
/// </summary>
/// <param name="appRepository"></param>
/// <param name="ruleRepository"></param>
/// <param name="uploadTokenRepository"></param>
/// <param name="validator"></param>
/// <param name="timeProvider"></param>
public sealed class OidcExchangeService(IStoreasyRepositoryBase<App> appRepository,
                                       IStoreasyRepositoryBase<TrustedPublisher> ruleRepository,
                                       IStoreasyRepositoryBase<UploadToken> uploadTokenRepository,
                                       IOidcTokenValidator validator,
                                       TimeProvider timeProvider) : IOidcExchangeService
{
    private const int _maxClaimLength = 300;
    private const int _maxClaims = 40;

    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<TrustedPublisher> _ruleRepository = ruleRepository;
    private readonly IStoreasyRepositoryBase<UploadToken> _uploadTokenRepository = uploadTokenRepository;
    private readonly IOidcTokenValidator _validator = validator;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<OidcExchangeDto>> ExchangeAsync(int appId, string token, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id, OrganizationId = a.OrganizationId },
                                                              cancellationToken: cancellationToken);

        var rules = app == null ? [] : await _ruleRepository.GetAllAsync(r => r.AppId == appId && r.OrganizationId == app.OrganizationId,
                                                                          projection: TrustedPublisherProjection,
                                                                          cancellationToken: cancellationToken);

        if (app == null || rules.Count == 0)
            return Response<OidcExchangeDto>.Error(null, MessageKey.AppNotFound);

        var extraIssuers = rules.Where(r => r.Provider == OidcProvider.GitLab && !string.IsNullOrWhiteSpace(r.Issuer)).Select(r => r.Issuer.TrimEnd('/')).ToList();
        var validation = await _validator.ValidateAsync(token, extraIssuers, cancellationToken);
        var now = Now();

        var candidates = rules.Where(r => IssuerFits(r, validation.Issuer)).ToList();

        if (!validation.Valid)
        {
            await RecordAsync(candidates.Select(r => r.Id).ToList(), OidcExchangeResult.InvalidToken, null, null, now, cancellationToken);

            return Response<OidcExchangeDto>.Error(null, MessageKey.OidcTokenInvalid);
        }

        if (candidates.Count > 0 && !candidates.Exists(r => validation.Audiences.Contains(TrustedPublisherMatcher.AudienceOf(r.Provider))))
        {
            await RecordAsync([.. candidates.Select(r => r.Id)], OidcExchangeResult.InvalidToken, SafeClaims(validation.Claims), null, now, cancellationToken);

            return Response<OidcExchangeDto>.Error(null, MessageKey.OidcTokenInvalid);
        }

        var claims = SafeClaims(validation.Claims);
        var runUrl = TrustedPublisherMatcher.RunUrlOf(validation.Claims);
        var matched = candidates.FirstOrDefault(r => TrustedPublisherMatcher.Matches(r, validation.Issuer, validation.Audiences, validation.Claims));

        await RecordAsync([.. candidates.Where(r => r != matched).Select(r => r.Id)], OidcExchangeResult.ClaimMismatch, claims, runUrl, now, cancellationToken);

        if (matched == null)
            return Response<OidcExchangeDto>.Error(null, MessageKey.OidcClaimMismatch);

        await RecordAsync([matched.Id], OidcExchangeResult.Succeeded, claims, runUrl, now, cancellationToken);

        var value = SecureTokens.Create(TokenPrefixes.UploadToken);

        var uploadToken = new UploadToken
        {
            OrganizationId = app.OrganizationId,
            AppId = app.Id,
            TrustedPublisherId = matched.Id,
            TokenHash = SecureTokens.Hash(value),
            Prefix = TokenPrefixes.DisplayPrefixOf(value),
            ExpiryDate = now.Add(UploadToken.Lifetime),
            RunUrl = runUrl is { Length: > 500 } ? runUrl[..500] : runUrl,
        };

        await _uploadTokenRepository.AddAsync(uploadToken, cancellationToken);

        return Response<OidcExchangeDto>.Success(new OidcExchangeDto { UploadToken = value, ExpiryDate = uploadToken.ExpiryDate, AppId = app.Id });
    }

    /// <inheritdoc/>
    public async Task<Response<TrustedPublisherTestDto>> TestAsync(int organizationId, int ruleId, string token, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetFirstOrDefaultAsync(r => r.Id == ruleId && r.OrganizationId == organizationId,
                                                                projection: TrustedPublisherProjection,
                                                                cancellationToken: cancellationToken);

        if (rule == null)
            return Response<TrustedPublisherTestDto>.Error(null, MessageKey.TrustedPublisherNotFound);

        var extraIssuers = string.IsNullOrWhiteSpace(rule.Issuer) ? [] : new[] { rule.Issuer.TrimEnd('/') };
        var validation = await _validator.ValidateAsync(token, extraIssuers, cancellationToken);

        if (!validation.Valid)
            return Response<TrustedPublisherTestDto>.Success(new TrustedPublisherTestDto { Valid = false, Matched = false, Error = validation.Error });

        var comparison = TrustedPublisherMatcher.Compare(rule, validation.Issuer, validation.Audiences, validation.Claims);

        return Response<TrustedPublisherTestDto>.Success(new TrustedPublisherTestDto
        {
            Valid = true,
            Matched = comparison.TrueForAll(c => c.Matches),
            Claims = [.. comparison.Select(c => new ClaimComparisonDto { Claim = c.Claim, Expected = c.Expected, Actual = c.Actual, Matches = c.Matches })],
        });
    }

    private static System.Linq.Expressions.Expression<Func<TrustedPublisher, TrustedPublisher>> TrustedPublisherProjection => r => new TrustedPublisher
    {
        Id = r.Id,
        AppId = r.AppId,
        OrganizationId = r.OrganizationId,
        Provider = r.Provider,
        Repository = r.Repository,
        Workflow = r.Workflow,
        Branch = r.Branch,
        Environment = r.Environment,
        AzureOrganization = r.AzureOrganization,
        AzureProject = r.AzureProject,
        ServiceConnection = r.ServiceConnection,
        Issuer = r.Issuer,
    };

    private static bool IssuerFits(TrustedPublisher rule, string issuer)
    {
        if (issuer == null)
            return true;

        return rule.Provider switch
        {
            OidcProvider.GitHub => issuer == TrustedPublisherMatcher.GitHubIssuer,
            OidcProvider.AzureDevOps => TrustedPublisherMatcher.IsAzureIssuer(issuer),
            _ => issuer == TrustedPublisherMatcher.IssuerOf(rule),
        };
    }

    private static List<OidcClaim> SafeClaims(IReadOnlyDictionary<string, string> claims)
        => [.. claims.Take(_maxClaims).Select(c => new OidcClaim { Name = c.Key, Value = c.Value is { Length: > _maxClaimLength } ? c.Value[.._maxClaimLength] : c.Value })];

    private Task RecordAsync(List<int> ruleIds, OidcExchangeResult result, List<OidcClaim> claims, string runUrl, DateTime now, CancellationToken cancellationToken)
    {
        if (ruleIds.Count == 0)
            return Task.CompletedTask;

        var rows = ruleIds.Select(id => new TrustedPublisher
        {
            Id = id,
            LastExchangeDate = now,
            LastExchangeResult = result,
            LastExchangeRunUrl = runUrl is { Length: > 500 } ? runUrl[..500] : runUrl,
            LastClaims = claims,
        }).ToList();

        return _ruleRepository.UpdateAsync(rows, cancellationToken, r => r.LastExchangeDate, r => r.LastExchangeResult, r => r.LastExchangeRunUrl, r => r.LastClaims);
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
