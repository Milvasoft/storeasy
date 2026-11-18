using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.UpdateMockupTemplate;

/// <summary>
/// Handles <see cref="UpdateMockupTemplateCommand"/>. The version check is part of the update's condition, so two
/// concurrent saves of the same version cannot both win. The slot set is replaced and captions of removed slots or
/// locales are deleted in the same transaction. Every screenshot and the background image must be an uploaded asset of
/// the organization.
/// </summary>
/// <param name="TemplateRepository"></param>
/// <param name="SlotRepository"></param>
/// <param name="CaptionRepository"></param>
/// <param name="AssetRepository"></param>
/// <param name="TransactionRunner"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateMockupTemplateCommandHandler(IStoreasyRepositoryBase<MockupTemplate> TemplateRepository,
                                                 IStoreasyRepositoryBase<MockupSlot> SlotRepository,
                                                 IStoreasyRepositoryBase<MockupCaption> CaptionRepository,
                                                 IStoreasyRepositoryBase<Asset> AssetRepository,
                                                 ITransactionRunner TransactionRunner,
                                                 IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateMockupTemplateCommand, int>
{
    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = TemplateRepository;
    private readonly IStoreasyRepositoryBase<MockupSlot> _slotRepository = SlotRepository;
    private readonly IStoreasyRepositoryBase<MockupCaption> _captionRepository = CaptionRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateMockupTemplateCommand request, CancellationToken cancellationToken)
    {
        var current = await _templateRepository.GetFirstOrDefaultAsync(t => t.Id == request.TemplateId && t.OrganizationId == request.OrganizationId,
                                                                       projection: t => new MockupTemplate { Id = t.Id, AppId = t.AppId, Version = t.Version },
                                                                       cancellationToken: cancellationToken);

        if (current == null)
            return Response<int>.Error(0, MessageKey.MockupTemplateNotFound);

        if (current.Version != request.Version)
            return Response<int>.Error(0, MessageKey.MockupTemplateChanged);

        var name = request.Name.Trim();
        var taken = await _templateRepository.GetFirstOrDefaultAsync(t => t.AppId == current.AppId && t.Name == name && t.Id != current.Id,
                                                                     projection: t => new MockupTemplate { Id = t.Id },
                                                                     cancellationToken: cancellationToken);

        if (taken != null)
            return Response<int>.Error(0, MessageKey.MockupTemplateNameExists);

        var spec = MockupSpecRules.Upgrade(request.Spec);
        var assetIds = (request.Slots ?? []).Select(s => s.AssetId).ToList();

        if (spec.Background.Kind == MockupSpecRules.BackgroundKinds.Image && spec.Background.AssetId is long backgroundId)
            assetIds.Add(backgroundId);

        assetIds = [.. assetIds.Distinct()];

        var known = await _assetRepository.GetUploadedAssetKeysAsync(request.OrganizationId, assetIds, cancellationToken);

        if (known.Count != assetIds.Count)
            return Response<int>.Error(0, MessageKey.MockupAssetInvalid);

        var locales = request.Locales.Select(l => l.Trim()).ToList();
        var primary = locales.First(l => string.Equals(l, request.PrimaryLocale.Trim(), StringComparison.OrdinalIgnoreCase));
        var nextVersion = request.Version + 1;
        var slots = (request.Slots ?? []).Select(s => new MockupSlot
        {
            MockupTemplateId = current.Id,
            Order = s.Order,
            Family = s.Family,
            Locale = s.Locale == null ? null : locales.First(l => string.Equals(l, s.Locale, StringComparison.OrdinalIgnoreCase)),
            AssetId = s.AssetId,
        }).ToList();

        var updated = 0;

        await _transactionRunner.RunAsync(async () =>
        {
            var builder = new SetPropertyBuilder<MockupTemplate>().SetPropertyValue(t => t.Name, name)
                                                                  .SetPropertyValue(t => t.Spec, spec)
                                                                  .SetPropertyValue(t => t.Locales, locales)
                                                                  .SetPropertyValue(t => t.PrimaryLocale, primary)
                                                                  .SetPropertyValue(t => t.SlotCount, request.SlotCount)
                                                                  .SetPropertyValue(t => t.Version, nextVersion);

            updated = await _templateRepository.ExecuteUpdateAsync(t => t.Id == current.Id && t.Version == request.Version, builder, cancellationToken);

            if (updated == 0)
                return;

            await _slotRepository.ExecuteDeleteAsync(s => s.MockupTemplateId == current.Id, cancellationToken: cancellationToken);

            if (slots.Count > 0)
                await _slotRepository.AddRangeAsync(slots, cancellationToken);

            await _captionRepository.ExecuteDeleteAsync(c => c.MockupTemplateId == current.Id && (c.SlotOrder > request.SlotCount || !locales.Contains(c.Locale)), cancellationToken: cancellationToken);
        });

        if (updated == 0)
            return Response<int>.Error(0, MessageKey.MockupTemplateChanged);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MockupTemplateUpdated,
                                     current.Id.ToString(),
                                     $"Mockup template '{name}' saved as version {nextVersion} ({request.SlotCount} slots, {locales.Count} locales).",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(nextVersion);
    }
}
