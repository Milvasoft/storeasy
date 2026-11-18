using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.GetMsProductDetail;

/// <summary>
/// Handles <see cref="GetMsProductDetailQuery"/>. A record of another organization or another store is reported as not
/// found. The active submission is the newest pending one of the app (or the MSI/EXE product).
/// </summary>
/// <param name="productRepository"></param>
/// <param name="submissionRepository"></param>
/// <param name="addonRepository"></param>
/// <param name="options"></param>
/// <param name="localizer"></param>
public class GetMsProductDetailQueryHandler(IStoreasyRepositoryBase<MsProduct> productRepository,
                                            IStoreasyRepositoryBase<MsSubmission> submissionRepository,
                                            IStoreasyRepositoryBase<MsAddon> addonRepository,
                                            MicrosoftStoreOptions options,
                                            IMilvaLocalizer localizer) : IInterceptable, IQueryHandler<GetMsProductDetailQuery, MsProductDto>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = submissionRepository;
    private readonly IStoreasyRepositoryBase<MsAddon> _addonRepository = addonRepository;
    private readonly MicrosoftStoreOptions _options = options;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<Response<MsProductDto>> Handle(GetMsProductDetailQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetFirstOrDefaultAsync(p => p.StoreAppId == request.StoreAppId && p.OrganizationId == request.OrganizationId && p.StoreApp.Store == StoreKind.Microsoft && !p.StoreApp.App.IsDeleted,
                                                                      projection: MsProductDto.Projection,
                                                                      cancellationToken: cancellationToken);

        if (product == null)
            return Response<MsProductDto>.Error(null, MessageKey.StoreAppNotFound);

        var submissions = await _submissionRepository.GetAllAsync(s => s.StoreAppId == request.StoreAppId && (s.Target == MsSubmissionTarget.App || s.Target == MsSubmissionTarget.Installer),
                                                                  projection: MsSubmissionDto.Projection,
                                                                  cancellationToken: cancellationToken);

        var assistedAddon = await _addonRepository.GetFirstOrDefaultAsync(a => a.StoreAppId == request.StoreAppId && !a.ApiWritable, projection: a => (int?)a.Id, cancellationToken: cancellationToken);

        var newest = submissions.OrderByDescending(s => s.Id).ToList();

        product.ActiveSubmission = newest.FirstOrDefault(s => s.IsPending);
        product.Notices = [_localizer[MessageKey.MsNoticeSingleSubmission], _localizer[MessageKey.MsNoticeApiOnly]];
        product.AssistedSteps = MsAssistedSteps.For(product,
                                                    assistedAddon.HasValue,
                                                    newest.FirstOrDefault()?.Status == MsSubmissionStatus.CertificationFailed,
                                                    _options.PartnerCenterProductUrl,
                                                    key => _localizer[key]);

        return Response<MsProductDto>.Success(product);
    }
}
