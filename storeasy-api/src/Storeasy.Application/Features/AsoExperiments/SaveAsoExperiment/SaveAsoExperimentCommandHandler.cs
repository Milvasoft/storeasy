using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;
using System.Globalization;

namespace Storeasy.Application.Features.AsoExperiments.SaveAsoExperiment;

/// <summary>
/// Handles <see cref="SaveAsoExperimentCommand"/>: checks the store record (it must belong to the app and its store must
/// support the kind), the variants (<see cref="AsoExperimentPlanner.Validate"/>) and that every asset they use is an
/// uploaded asset of the organization, then saves and audits.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="AssetRepository"></param>
/// <param name="PlanRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="ContentService"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
[Log]
public record SaveAsoExperimentCommandHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                              IStoreasyRepositoryBase<Asset> AssetRepository,
                                              IStoreasyRepositoryBase<ChangePlan> PlanRepository,
                                              IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                              IStoreContentService ContentService,
                                              IAuditTrail AuditTrail,
                                              ICurrentUser CurrentUser,
                                              IMilvaLocalizer Localizer,
                                              TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SaveAsoExperimentCommand, AsoExperimentDto>
{
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = PlanRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AsoExperimentDto>> Handle(SaveAsoExperimentCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        AsoExperiment existing = null;

        if (request.Id != null)
        {
            existing = await _experimentRepository.FindAsync(request.OrganizationId, request.Id.Value, cancellationToken);

            if (existing == null)
                return Response<AsoExperimentDto>.Error(null, MessageKey.AsoExperimentNotFound);

            if (!AsoExperimentRules.Editable(existing.Kind, existing.Status))
                return Response<AsoExperimentDto>.Error(null, MessageKey.AsoExperimentNotEditable);
        }
        else
        {
            var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

            if (app == null || app.AppId != request.AppId)
                return Response<AsoExperimentDto>.Error(null, MessageKey.StoreAppNotFound);

            if (AsoExperimentRules.Support(app.Store, request.Kind) == AsoStoreSupport.NotSupported)
                return Response<AsoExperimentDto>.Error(null, new ResponseMessage(MessageKey.AsoKindNotSupported, _localizer[MessageKey.AsoKindNotSupported, app.Store.ToString()]));

            existing = new AsoExperiment
            {
                OrganizationId = request.OrganizationId,
                AppId = app.AppId,
                StoreAppId = app.StoreAppId,
                Store = app.Store,
                Kind = request.Kind,
                Status = AsoExperimentStatus.Draft,
            };
        }

        var variants = Normalize(request.Variants, existing.Variants);
        var traffic = AsoExperimentRules.HasTraffic(existing.Kind) ? request.TrafficProportion : null;
        var (key, detail) = AsoExperimentPlanner.Validate(existing.Kind, traffic, variants);

        if (key != null)
            return Response<AsoExperimentDto>.Error(null, new ResponseMessage(key, _localizer[key, detail ?? string.Empty]));

        var assetIds = AsoExperimentPlanner.AssetIdsOf(variants);

        if (assetIds.Count > 0)
        {
            var found = await _assetRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId && a.Status == AssetStatus.Uploaded && assetIds.Contains(a.Id),
                                                           projection: a => new Asset { Id = a.Id },
                                                           cancellationToken: cancellationToken);

            if (found.Count != assetIds.Count)
                return Response<AsoExperimentDto>.Error(null, MessageKey.AssetNotFound);
        }

        var name = request.Name.Trim();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (existing.Id == 0)
        {
            existing.Name = name;
            existing.TrafficProportion = traffic;
            existing.Variants = variants;
            existing.Note = note;
            existing.CreationDate = now;
            existing.CreatorUserName = _currentUser.UserName;

            await _experimentRepository.AddAsync(existing, cancellationToken);
        }
        else
        {
            await _experimentRepository.ExecuteUpdateAsync(existing.Id,
                                                           new SetPropertyBuilder<AsoExperiment>().SetPropertyValue(r => r.Name, name)
                                                                                                   .SetPropertyValue(r => r.TrafficProportion, traffic)
                                                                                                   .SetPropertyValue(r => r.Variants, variants)
                                                                                                   .SetPropertyValue(r => r.Note, note),
                                                           cancellationToken);
        }

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     request.Id == null ? AuditAction.AsoExperimentCreated : AuditAction.AsoExperimentUpdated,
                                     existing.Id.ToString(CultureInfo.InvariantCulture),
                                     $"{existing.Kind} '{name}' {(request.Id == null ? "created" : "updated")} with {variants.Count} variants.",
                                     existing.Store,
                                     new { existing.Id, existing.Kind, existing.StoreAppId, Variants = variants.Count },
                                     cancellationToken);

        var dto = await _experimentRepository.DetailAsync(_planRepository, _storeAppRepository, request.OrganizationId, existing.Id, now, _localizer, cancellationToken);

        return Response<AsoExperimentDto>.Success(dto);
    }

    /// <summary>
    /// Trims the variants and keeps the store ids of the existing ones by key (a client never sets them).
    /// </summary>
    private static List<AsoExperimentVariant> Normalize(List<AsoExperimentVariant> requested, List<AsoExperimentVariant> existing)
        => [.. (requested ?? []).Select(v => new AsoExperimentVariant
        {
            Key = v.Key?.Trim(),
            Name = v.Name?.Trim(),
            AppIconName = string.IsNullOrWhiteSpace(v.AppIconName) ? null : v.AppIconName.Trim(),
            ExternalId = existing?.FirstOrDefault(e => e.Key == v.Key?.Trim())?.ExternalId,
            Locales = [.. (v.Locales ?? []).Select(l => new AsoVariantLocale
            {
                Locale = l.Locale?.Trim(),
                PromotionalText = Blank(l.PromotionalText),
                ShortDescription = Blank(l.ShortDescription),
                FullDescription = Blank(l.FullDescription),
                Media = [.. (l.Media ?? []).Where(m => m.AssetIds?.Count > 0).Select(m => new AsoVariantMedia { Kind = m.Kind, ClassKey = m.ClassKey?.Trim(), AssetIds = [.. m.AssetIds] })],
            })],
        })];

    private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
