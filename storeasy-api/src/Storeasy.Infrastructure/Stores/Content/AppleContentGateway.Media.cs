using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// Screenshot and app preview sets of the App Store content engine (store-ops <c>syncScreenshots</c>).
/// </summary>
public sealed partial class AppleContentGateway
{
    /// <summary>
    /// Makes the set of (version, locale, class) hold the desired items in order: items whose MD5 is already in the set are
    /// kept, the others are deleted, the missing ones uploaded, then the set is reordered. An empty desired list deletes the
    /// set.
    /// </summary>
    private async Task<string> SyncMediaSetAsync(AppStoreConnectApi api, string versionId, MediaSetChange change, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var localizationId = await ContentSteps.RunAsync(context, "versionLocalization.ensure", change.Locale, () => EnsureVersionLocalizationAsync(api, versionId, change.Locale, cancellationToken), cancellationToken);

        return await SyncMediaSetAsync(api, AppleMediaParent.VersionLocalization(localizationId), change, context, cancellationToken);
    }

    /// <summary>
    /// Makes the media set of <paramref name="change"/> under <paramref name="parent"/> (a version localization, an
    /// experiment treatment localization or a custom product page localization) hold the desired items in order (see the
    /// version overload).
    /// </summary>
    /// <param name="api"></param>
    /// <param name="parent"></param>
    /// <param name="change"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> SyncMediaSetAsync(AppStoreConnectApi api, AppleMediaParent parent, MediaSetChange change, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var screenshot = change.Kind == MediaKind.Screenshot;
        var (setType, itemType, setsRelation, typeAttribute) = screenshot
            ? ("appScreenshotSets", "appScreenshots", "appScreenshotSets", "screenshotDisplayType")
            : ("appPreviewSets", "appPreviews", "appPreviewSets", "previewType");
        var label = $"{change.Locale} {change.ClassKey}";

        var set = (await ListSetsAsync(api, parent, change.Kind, cancellationToken)).FirstOrDefault(s => s.ClassKey == change.ClassKey);

        if (change.Items.Count == 0)
        {
            if (set == null)
                return $"{label}: nothing to delete.";

            return await ContentSteps.RunAsync(context, "set.delete", label, async () =>
            {
                await api.DeleteAsync($"/v1/{setType}/{set.Id}", cancellationToken);
                return $"{label}: set deleted.";
            }, cancellationToken);
        }

        if (set == null)
        {
            var setId = await ContentSteps.RunAsync(context, "set.create", label, () => CreateSetAsync(api, parent, change, setType, setsRelation, typeAttribute, cancellationToken), cancellationToken);

            set = new MediaSetState { Kind = change.Kind, ClassKey = change.ClassKey, Locale = change.Locale, Id = setId };
        }

        var plan = PlanSet(set.Items, change.Items);

        foreach (var removed in plan.Remove)
        {
            await ContentSteps.RunAsync(context, "asset.delete", removed.FileName ?? removed.Id, async () =>
            {
                await api.DeleteAsync($"/v1/{itemType}/{removed.Id}", cancellationToken);
                return null;
            }, cancellationToken);
        }

        var uploadedIds = new Dictionary<MediaItemChange, string>();

        foreach (var item in plan.Upload)
            uploadedIds[item] = await UploadItemAsync(api, set.Id, item, change, itemType, setsRelation, context, cancellationToken);

        var order = change.Items.Select(i => plan.Keep.TryGetValue(i, out var kept) ? kept : uploadedIds.GetValueOrDefault(i)).Where(id => id != null).ToList();
        var currentOrder = set.Items.Select(i => i.Id).Where(id => !plan.Remove.Any(r => r.Id == id)).ToList();

        if (plan.Upload.Count > 0 || !order.SequenceEqual(currentOrder))
        {
            await ContentSteps.RunAsync(context, "set.reorder", label, async () =>
            {
                using var _ = await api.PatchAsync($"/v1/{setType}/{set.Id}/relationships/{itemType}", new { data = order.Select(id => new { type = itemType, id }).ToList() }, cancellationToken);
                return $"{order.Count} items in order.";
            }, cancellationToken);
        }

        return $"{label}: {plan.Keep.Count} kept, {plan.Upload.Count} uploaded, {plan.Remove.Count} deleted.";
    }

    private static async Task<string> CreateSetAsync(AppStoreConnectApi api,
                                                     AppleMediaParent parent,
                                                     MediaSetChange change,
                                                     string setType,
                                                     string setsRelation,
                                                     string typeAttribute,
                                                     CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = setType,
                ["attributes"] = new JsonObject { [typeAttribute] = change.ClassKey },
                ["relationships"] = new JsonObject { [parent.Relation] = RelationNode(parent.Type, parent.Id) },
            },
        };

        try
        {
            // Apple has answered 500 on set creation; a retry is safe because the set is looked up again afterwards.
            using var created = await api.PostAsync($"/v1/{setsRelation}", body, cancellationToken, retry: true);

            return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
        }
        catch (StoreCallException ex) when (ex.Transient || ex.StatusCode == 409)
        {
            var again = (await ListSetsAsync(api, parent, change.Kind, cancellationToken)).FirstOrDefault(s => s.ClassKey == change.ClassKey);

            if (again == null)
                throw;

            return again.Id;
        }
    }

    /// <summary>
    /// Matches desired items to the set's items by checksum (failed items never match): matched items are kept, the
    /// others uploaded, the set's unmatched items removed.
    /// </summary>
    /// <param name="existing"></param>
    /// <param name="desired"></param>
    /// <returns></returns>
    public static (Dictionary<MediaItemChange, string> Keep, List<MediaItemChange> Upload, List<MediaItemState> Remove) PlanSet(IReadOnlyList<MediaItemState> existing, IReadOnlyList<MediaItemChange> desired)
    {
        var pool = existing.ToList();
        var keep = new Dictionary<MediaItemChange, string>();
        var upload = new List<MediaItemChange>();

        foreach (var item in desired)
        {
            var index = item.ExistingId != null
                ? pool.FindIndex(e => e.Id == item.ExistingId)
                : pool.FindIndex(e => e.Checksum != null && item.Md5 != null && string.Equals(e.Checksum, item.Md5, StringComparison.OrdinalIgnoreCase) && e.State != "FAILED");

            if (index >= 0)
            {
                keep[item] = pool[index].Id;
                pool.RemoveAt(index);
            }
            else if (item.AssetId != null)
            {
                upload.Add(item);
            }
        }

        return (keep, upload, pool);
    }

    /// <summary>
    /// Reserves, uploads and commits one item; a failure after the reservation deletes it so no half reserved item stays
    /// in the set. Waits for processing up to the configured time and reports the state reached.
    /// </summary>
    private async Task<string> UploadItemAsync(AppStoreConnectApi api,
                                               string setId,
                                               MediaItemChange item,
                                               MediaSetChange change,
                                               string itemType,
                                               string setsRelation,
                                               IStoreJobContext context,
                                               CancellationToken cancellationToken)
    {
        var data = await ContentSteps.ReadObjectAsync(_fileStorage, item.ObjectKey, cancellationToken);
        var setRelation = change.Kind == MediaKind.Screenshot ? "appScreenshotSet" : "appPreviewSet";

        var reservation = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = itemType,
                ["attributes"] = change.Kind == MediaKind.Screenshot
                    ? new JsonObject { ["fileName"] = item.FileName, ["fileSize"] = data.Length }
                    : new JsonObject { ["fileName"] = item.FileName, ["fileSize"] = data.Length, ["mimeType"] = item.ContentType ?? "video/mp4" },
                ["relationships"] = new JsonObject { [setRelation] = RelationNode(setsRelation, setId) },
            },
        };

        var id = await ContentSteps.RunAsync(context, "asset.reserve", item.FileName, async () =>
        {
            using var reserved = await api.PostAsync($"/v1/{itemType}", reservation, cancellationToken);
            var resource = JsonRead.Get(reserved.RootElement, "data");
            var reservedId = JsonRead.Str(resource, "id");

            try
            {
                await api.UploadAsync(resource, data, cancellationToken);
            }
            catch
            {
                await api.DeleteAsync($"/v1/{itemType}/{reservedId}", CancellationToken.None);
                throw;
            }

            return reservedId;
        }, cancellationToken);

        await ContentSteps.RunAsync(context, "asset.commit", item.FileName, async () =>
        {
            var attributes = new JsonObject { ["uploaded"] = true, ["sourceFileChecksum"] = item.Md5 };

            if (change.Kind == MediaKind.Preview && change.PreviewFrameTimeCode != null)
                attributes["previewFrameTimeCode"] = change.PreviewFrameTimeCode;

            try
            {
                using var _ = await api.PatchAsync($"/v1/{itemType}/{id}", Resource(itemType, id, attributes), cancellationToken);
            }
            catch
            {
                await api.DeleteAsync($"/v1/{itemType}/{id}", CancellationToken.None);
                throw;
            }

            return null;
        }, cancellationToken);

        await ContentSteps.RunAsync(context, "asset.processing", item.FileName, () => WaitForAssetAsync(api, $"/v1/{itemType}/{id}", cancellationToken), cancellationToken, StepDirection.Inbound);

        return id;
    }

    private async Task<string> WaitForAssetAsync(AppStoreConnectApi api, string path, CancellationToken cancellationToken)
    {
        var deadline = _http.Clock.GetUtcNow().AddSeconds(_http.Options.AssetPollTimeoutSeconds);

        while (true)
        {
            using var document = await api.GetAsync(path, cancellationToken);
            var item = ToItem(JsonRead.Str(JsonRead.Get(document.RootElement, "data"), "id"), JsonRead.Get(document.RootElement, "data"));

            if (item.State == "COMPLETE")
                return "COMPLETE";

            if (item.State == "FAILED")
            {
                var errors = JsonRead.Get(JsonRead.Get(JsonRead.Get(JsonRead.Get(document.RootElement, "data"), "attributes"), "assetDeliveryState"), "errors");

                throw new StoreCallException($"Apple failed to process {item.FileName ?? item.Id}: {(errors.ValueKind == JsonValueKind.Undefined ? "no detail" : errors.GetRawText())}", null, false);
            }

            if (_http.Clock.GetUtcNow() >= deadline)
                return $"Still {item.State ?? "processing"}; Apple keeps processing it.";

            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(10, _http.Options.AssetPollIntervalMilliseconds)), _http.Clock, cancellationToken);
        }
    }
}

/// <summary>
/// The resource screenshot and preview sets hang under: its relationship name on the set, its type and its id.
/// </summary>
/// <param name="Relation">Relationship name on the set (e.g. appStoreVersionLocalization).</param>
/// <param name="Type">Resource type (e.g. appStoreVersionLocalizations).</param>
/// <param name="Id">Resource id.</param>
public sealed record AppleMediaParent(string Relation, string Type, string Id)
{
    /// <summary>
    /// An App Store version localization.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static AppleMediaParent VersionLocalization(string id) => new("appStoreVersionLocalization", "appStoreVersionLocalizations", id);

    /// <summary>
    /// A product page optimization treatment localization.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static AppleMediaParent TreatmentLocalization(string id) => new("appStoreVersionExperimentTreatmentLocalization", "appStoreVersionExperimentTreatmentLocalizations", id);

    /// <summary>
    /// A custom product page localization.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static AppleMediaParent CustomPageLocalization(string id) => new("appCustomProductPageLocalization", "appCustomProductPageLocalizations", id);
}
