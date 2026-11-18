using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.MsStore;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// The <see cref="StoreJobKind.MsCheckInstaller"/> job: downloads the installers of an MSI/EXE product (or the given
/// addresses) and records their Authenticode check. Only the product's own package rows are updated.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="microsoftStoreClient"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class MsCheckInstallerJobHandler(IStoreasyRepositoryBase<MsInstallerPackage> packageRepository,
                                               IMicrosoftStoreClient microsoftStoreClient,
                                               MicrosoftStoreOptions options,
                                               TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _packageRepository = packageRepository;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = microsoftStoreClient;
    private readonly MicrosoftStoreOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.MsCheckInstaller;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<MsCheckInstallerPayload>() ?? throw new StoreJobException("The job has no product.", transient: false);
        var rows = await _packageRepository.GetAllAsync(p => p.StoreAppId == payload.StoreAppId && p.OrganizationId == context.OrganizationId, cancellationToken: cancellationToken);
        var urls = payload.PackageUrls?.Count > 0 ? payload.PackageUrls.Distinct(StringComparer.Ordinal).ToList() : [.. rows.Select(r => r.PackageUrl).Distinct(StringComparer.Ordinal)];
        var step = await context.BeginStepAsync("installers.check", StepDirection.Inbound, total: urls.Count, cancellationToken: cancellationToken);
        var results = new Dictionary<string, (MsSignatureStatus Status, string Note)>(StringComparer.Ordinal);

        for (var i = 0; i < urls.Count; i++)
        {
            results[urls[i]] = await MsJobSupport.CheckInstallerAsync(_microsoftStoreClient, urls[i], _options.InstallerCheckMaxBytes, cancellationToken);
            await context.ReportProgressAsync(step, i + 1, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var checkedRows = rows.Where(r => results.ContainsKey(r.PackageUrl)).ToList();

        foreach (var row in checkedRows)
        {
            row.SignatureStatus = results[row.PackageUrl].Status;
            row.SignatureNote = results[row.PackageUrl].Note?.Length > MsInstallerPackage.SignatureNoteMaxLength ? results[row.PackageUrl].Note[..MsInstallerPackage.SignatureNoteMaxLength] : results[row.PackageUrl].Note;
            row.CheckedDate = now;
        }

        if (checkedRows.Count > 0)
            await _packageRepository.UpdateAsync(checkedRows, cancellationToken, p => p.SignatureStatus, p => p.SignatureNote, p => p.CheckedDate);

        var signed = results.Count(r => r.Value.Status == MsSignatureStatus.Signed);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{signed} of {results.Count} signed.", cancellationToken);

        return new StoreJobOutcome($"{signed} of {results.Count} installers signed.", results.Select(r => new { Url = r.Key, r.Value.Status, r.Value.Note }).ToList());
    }
}
