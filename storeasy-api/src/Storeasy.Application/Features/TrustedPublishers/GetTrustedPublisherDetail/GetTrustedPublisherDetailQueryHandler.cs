using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.TrustedPublishers.GetTrustedPublisherDetail;

/// <summary>
/// Handles <see cref="GetTrustedPublisherDetailQuery"/>.
/// </summary>
/// <param name="RuleRepository"></param>
public record GetTrustedPublisherDetailQueryHandler(IStoreasyRepositoryBase<TrustedPublisher> RuleRepository) : IInterceptable, IQueryHandler<GetTrustedPublisherDetailQuery, TrustedPublisherDto>
{
    private readonly IStoreasyRepositoryBase<TrustedPublisher> _ruleRepository = RuleRepository;

    /// <inheritdoc/>
    public async Task<Response<TrustedPublisherDto>> Handle(GetTrustedPublisherDetailQuery request, CancellationToken cancellationToken)
    {
        var rule = await _ruleRepository.GetFirstOrDefaultAsync(r => r.Id == request.Id && r.OrganizationId == request.OrganizationId,
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
                                                                    LastClaims = r.LastClaims,
                                                                },
                                                                cancellationToken: cancellationToken);

        return rule == null ? Response<TrustedPublisherDto>.Error(null, MessageKey.TrustedPublisherNotFound) : Response<TrustedPublisherDto>.Success(TrustedPublisherDtoExtensions.WithExpectedClaims(rule));
    }
}
