using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.DeliveryRules.GetDeliveryRules;

/// <summary>
/// Handles <see cref="GetDeliveryRulesQuery"/>.
/// </summary>
/// <param name="RuleRepository"></param>
/// <param name="AppRepository"></param>
public record GetDeliveryRulesQueryHandler(IStoreasyRepositoryBase<DeliveryRule> RuleRepository, IStoreasyRepositoryBase<App> AppRepository) : IInterceptable, IQueryHandler<GetDeliveryRulesQuery, DeliveryRulesDto>
{
    private readonly IStoreasyRepositoryBase<DeliveryRule> _ruleRepository = RuleRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;

    /// <inheritdoc/>
    public async Task<Response<DeliveryRulesDto>> Handle(GetDeliveryRulesQuery request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<DeliveryRulesDto>.Error(null, MessageKey.AppNotFound);

        var rules = await _ruleRepository.GetAllAsync(r => r.AppId == request.AppId && r.OrganizationId == request.OrganizationId,
                                                      projection: DeliveryRuleDto.EntityProjection,
                                                      cancellationToken: cancellationToken);

        var latest = rules.OrderByDescending(r => r.UpdatedDate).FirstOrDefault();

        return Response<DeliveryRulesDto>.Success(new DeliveryRulesDto
        {
            AppId = request.AppId,
            Rules = [.. rules.OrderBy(r => r.Platform).Select(DeliveryRuleDto.From)],
            UpdatedByUserName = latest?.UpdatedByUserName,
            UpdatedDate = latest?.UpdatedDate,
        });
    }
}
