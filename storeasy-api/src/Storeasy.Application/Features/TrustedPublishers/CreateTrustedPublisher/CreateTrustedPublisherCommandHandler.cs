using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.TrustedPublishers.CreateTrustedPublisher;

/// <summary>
/// Handles <see cref="CreateTrustedPublisherCommand"/>.
/// </summary>
/// <param name="RuleRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
[Log]
public record CreateTrustedPublisherCommandHandler(IStoreasyRepositoryBase<TrustedPublisher> RuleRepository,
                                                   IStoreasyRepositoryBase<App> AppRepository,
                                                   ICurrentUser CurrentUser,
                                                   IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<CreateTrustedPublisherCommand, int>
{
    private readonly IStoreasyRepositoryBase<TrustedPublisher> _ruleRepository = RuleRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CreateTrustedPublisherCommand request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        var azure = request.Provider == OidcProvider.AzureDevOps;

        var rule = new TrustedPublisher
        {
            OrganizationId = request.OrganizationId,
            AppId = request.AppId,
            Provider = request.Provider,
            Repository = azure ? null : Clean(request.Repository),
            Workflow = request.Provider == OidcProvider.GitHub ? Clean(request.Workflow) : null,
            Branch = azure ? null : Clean(request.Branch),
            Environment = azure ? null : Clean(request.Environment),
            AzureOrganization = azure ? Clean(request.Organization) : null,
            AzureProject = azure ? Clean(request.Project) : null,
            ServiceConnection = azure ? Clean(request.ServiceConnection) : null,
            Issuer = request.Provider == OidcProvider.GitLab ? Clean(request.Issuer)?.TrimEnd('/') : null,
            CreatedByUserId = _currentUser.Id,
            CreatedByUserName = _currentUser.UserName,
        };

        await _ruleRepository.AddAsync(rule, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.TrustedPublisherAdded,
                                     rule.Id.ToString(),
                                     $"Trusted publisher {rule.Provider} added for app {rule.AppId}.",
                                     details: TrustedPublisherMatcher.ExpectedClaims(rule),
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(rule.Id);
    }

    private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
