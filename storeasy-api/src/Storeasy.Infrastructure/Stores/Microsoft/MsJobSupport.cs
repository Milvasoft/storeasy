using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Enums;
using System.IO.Compression;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// File work of the Microsoft write jobs: the ZIP archive of a submission's new files, built from storeasy's uploaded
/// assets, and the Authenticode check of MSI/EXE installers.
/// </summary>
public static class MsJobSupport
{
    /// <summary>
    /// Lifetime of the storage download addresses the job reads assets with.
    /// </summary>
    public static readonly TimeSpan AssetUrlLifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Builds the submission ZIP in a temporary file: every asset is downloaded from storage and stored under its entry
    /// name. The caller deletes the returned file.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="storage"></param>
    /// <param name="entries">Entry name and object key of each asset.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<string> BuildZipAsync(IMicrosoftStoreClient client, IFileStorage storage, IReadOnlyList<(string EntryName, string ObjectKey)> entries, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"storeasy-ms-{Guid.NewGuid():N}.zip");

        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true);
            await using var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false);

            foreach (var (entryName, objectKey) in entries)
            {
                // Packages are already compressed; storing keeps the job fast.
                var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);

                await using var target = await entry.OpenAsync(cancellationToken);

                await client.DownloadAsync(storage.CreateDownloadUrl(objectKey, AssetUrlLifetime), false, async (source, _) =>
                {
                    await source.CopyToAsync(target, cancellationToken);
                    return true;
                }, cancellationToken);
            }

            return path;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    /// <summary>
    /// Downloads an installer from its own address (public HTTPS hosts only) and checks its Authenticode signature.
    /// A download failure is <see cref="MsSignatureStatus.Unreachable"/>.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="url"></param>
    /// <param name="maxBytes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<(MsSignatureStatus Status, string Note)> CheckInstallerAsync(IMicrosoftStoreClient client, string url, long maxBytes, CancellationToken cancellationToken)
    {
        try
        {
            return await client.DownloadAsync(url, true, (stream, _) => AuthenticodeProbe.InspectAsync(stream, maxBytes, cancellationToken), cancellationToken);
        }
        catch (StoreCallException ex)
        {
            return (MsSignatureStatus.Unreachable, ex.Message);
        }
    }

    /// <summary>
    /// The submission address of an owner with a submission id.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="productId"></param>
    /// <param name="flightId"></param>
    /// <param name="addonStoreId"></param>
    /// <param name="submissionId"></param>
    /// <returns></returns>
    public static MsSubmissionRef Ref(MsSubmissionTarget target, string productId, string flightId, string addonStoreId, string submissionId = null) => new()
    {
        Target = target,
        ApplicationId = productId,
        FlightId = flightId,
        AddonStoreId = addonStoreId,
        SubmissionId = submissionId,
    };
}
