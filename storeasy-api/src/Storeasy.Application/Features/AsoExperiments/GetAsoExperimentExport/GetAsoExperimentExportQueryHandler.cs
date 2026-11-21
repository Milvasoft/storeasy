using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentExport;

/// <summary>
/// Handles <see cref="GetAsoExperimentExportQuery"/>. Download links live <see cref="LinkLifetime"/>.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="AssetRepository"></param>
/// <param name="FileStorage"></param>
public record GetAsoExperimentExportQueryHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                                 IStoreasyRepositoryBase<Asset> AssetRepository,
                                                 IFileStorage FileStorage) : IInterceptable, IQueryHandler<GetAsoExperimentExportQuery, AsoExperimentExportDto>
{
    /// <summary>
    /// Lifetime of the download links.
    /// </summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);

    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = AssetRepository;
    private readonly IFileStorage _fileStorage = FileStorage;

    /// <inheritdoc/>
    public async Task<Response<AsoExperimentExportDto>> Handle(GetAsoExperimentExportQuery request, CancellationToken cancellationToken)
    {
        var record = await _experimentRepository.FindAsync(request.OrganizationId, request.Id, cancellationToken);

        if (record == null)
            return Response<AsoExperimentExportDto>.Error(null, MessageKey.AsoExperimentNotFound);

        var variants = record.Variants ?? [];
        var assetIds = variants.SelectMany(v => v.Locales ?? []).SelectMany(l => l.Media ?? []).SelectMany(m => m.AssetIds ?? []).Distinct().ToList();
        var assets = assetIds.Count == 0
            ? []
            : await _assetRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId && a.Status == AssetStatus.Uploaded && assetIds.Contains(a.Id),
                                                 projection: a => new Asset { Id = a.Id, ObjectKey = a.ObjectKey, FileName = a.FileName },
                                                 cancellationToken: cancellationToken);
        var byId = assets.ToDictionary(a => a.Id);
        var export = new AsoExperimentExportDto();

        foreach (var variant in variants)
        {
            foreach (var locale in variant.Locales ?? [])
            {
                AddText(export, variant.Key, locale.Locale, "promotionalText", locale.PromotionalText);
                AddText(export, variant.Key, locale.Locale, "shortDescription", locale.ShortDescription);
                AddText(export, variant.Key, locale.Locale, "fullDescription", locale.FullDescription);

                foreach (var media in locale.Media ?? [])
                {
                    foreach (var assetId in media.AssetIds ?? [])
                    {
                        if (!byId.TryGetValue(assetId, out var asset))
                            continue;

                        export.Files.Add(new AsoExportFileDto
                        {
                            VariantKey = variant.Key,
                            Locale = locale.Locale,
                            ClassKey = media.ClassKey,
                            AssetId = asset.Id,
                            FileName = asset.FileName,
                            Url = _fileStorage.CreateDownloadUrl(asset.ObjectKey, LinkLifetime, asset.FileName),
                        });
                    }
                }
            }
        }

        return Response<AsoExperimentExportDto>.Success(export);
    }

    private static void AddText(AsoExperimentExportDto export, string variant, string locale, string field, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            export.Texts.Add(new AsoExportTextDto { VariantKey = variant, Locale = locale, Field = field, Value = value });
    }
}
