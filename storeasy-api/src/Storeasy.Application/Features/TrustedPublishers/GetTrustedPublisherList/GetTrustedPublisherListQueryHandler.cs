using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.TrustedPublishers.GetTrustedPublisherList;

/// <summary>
/// Handles <see cref="GetTrustedPublisherListQuery"/>.
/// </summary>
/// <param name="RuleRepository"></param>
public record GetTrustedPublisherListQueryHandler(IStoreasyRepositoryBase<TrustedPublisher> RuleRepository) : IInterceptable, IQueryHandler<GetTrustedPublisherListQuery, List<TrustedPublisherDto>>
{
    private readonly IStoreasyRepositoryBase<TrustedPublisher> _ruleRepository = RuleRepository;

    /// <inheritdoc/>
    public async Task<Response<List<TrustedPublisherDto>>> Handle(GetTrustedPublisherListQuery request, CancellationToken cancellationToken)
    {
        var rules = await _ruleRepository.GetAllAsync(r => r.OrganizationId == request.OrganizationId && (request.AppId == null || r.AppId == request.AppId),
                                                      projection: r => new TrustedPublisherDto
                                                      {
                                                                         Id = r.Id,
                                                                         AppId = r.AppId,
                                                                         AppName = r.App.Name,
                                                                         Provider = r.Provider,
                                                                         Repository = r.Repository,
                                                                         Workflow = r.Workflow,
                                                                         Organization = r.AzureOrganization,
                                                                         Project = r.AzureProject,
                                                                         ServiceConnection = r.ServiceConnection,
                                                                         Branch = r.Branch,
                                                                         Environment = r.Environment,
                                                                         Issuer = r.Issuer,
                                                                         LastExchangeDate = r.LastExchangeDate,
                                                                         LastExchangeResult = r.LastExchangeResult,
                                                                         LastExchangeRunUrl = r.LastExchangeRunUrl,
                                                                         CreatorUserName = r.CreatedByUserName,
                                                                         CreationDate = r.CreationDate,
                                                      },
                                                      cancellationToken: cancellationToken);

        return Response<List<TrustedPublisherDto>>.Success([.. rules.Select(TrustedPublisherDtoExtensions.WithExpectedClaims).OrderBy(r => r.AppName).ThenBy(r => r.Id)]);
    }
}
