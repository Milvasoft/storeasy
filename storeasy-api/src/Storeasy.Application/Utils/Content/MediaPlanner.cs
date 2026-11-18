using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// Plans of media sets (screenshots, previews, Play graphics): every uploaded file is checked against the catalog rule of
/// its class (exact size, format, alpha, count, Play dimension and aspect limits) and against the set (an MD5 already in the
/// set is skipped, so an unchanged image never goes to the store again). Rejected files carry a reason and, when they fit
/// another class, that class.
/// </summary>
public static class MediaPlanner
{
    /// <summary>
    /// Result of <see cref="Check"/> for an accepted file.
    /// </summary>
    public const string Accepted = "Accepted";

    /// <summary>
    /// Result of <see cref="Check"/> for a file that is already in the set.
    /// </summary>
    public const string Skipped = "Skipped";

    /// <summary>
    /// Result of <see cref="Check"/> for a file that does not fit.
    /// </summary>
    public const string Rejected = "Rejected";

    /// <summary>
    /// Desired set: existing items kept in <paramref name="order"/> (then the other existing ones not deleted), followed by
    /// the accepted files. Returns the draft (null when nothing was accepted and nothing else changes).
    /// </summary>
    /// <param name="store"></param>
    /// <param name="mediaClass"></param>
    /// <param name="locale"></param>
    /// <param name="snapshot"></param>
    /// <param name="order">Existing item ids in the desired order.</param>
    /// <param name="deletes">Existing item ids to remove.</param>
    /// <param name="files">Uploaded files to add.</param>
    /// <param name="previewFrameTimeCode"></param>
    /// <param name="catalog"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static PlanDraft Build(StoreKind store,
                                  MediaClass mediaClass,
                                  string locale,
                                  StoreContentSnapshot snapshot,
                                  IReadOnlyList<string> order,
                                  IReadOnlyCollection<string> deletes,
                                  IReadOnlyList<InspectedAsset> files,
                                  string previewFrameTimeCode,
                                  RuleCatalog catalog,
                                  IMilvaLocalizer localizer)
    {
        var set = snapshot.Media.FirstOrDefault(m => m.Kind == mediaClass.Kind && m.ClassKey == mediaClass.ClassKey && string.Equals(m.Locale, locale, StringComparison.OrdinalIgnoreCase));
        var existing = set?.Items ?? [];
        var rule = catalog.Find(mediaClass.RuleId);
        var max = mediaClass.Single ? 1 : rule?.Params?.MaxCount ?? 10;

        var kept = order.Select(id => existing.FirstOrDefault(e => e.Id == id))
                        .Where(e => e != null && !deletes.Contains(e.Id))
                        .Concat(existing.Where(e => !order.Contains(e.Id) && !deletes.Contains(e.Id)))
                        .Distinct()
                        .ToList();

        var checks = files.Select(f => (File: f, Check: Check(store, mediaClass, f, existing.Select(e => e.Checksum), catalog, localizer))).ToList();
        var accepted = checks.Where(c => c.Check.Result == Accepted).Select(c => c.File).DistinctBy(f => f.Md5).ToList();

        if (mediaClass.Single && accepted.Count > 0)
        {
            accepted = [accepted[^1]];
            kept = [];
        }

        var draft = new PlanDraft
        {
            Kind = mediaClass.Kind == MediaKind.Preview ? ChangePlanKind.Previews : ChangePlanKind.Screenshots,
            Title = $"{mediaClass.ClassKey} ({locale}): {accepted.Count} new, {existing.Count - kept.Count} removed",
            VersionId = snapshot.VersionId,
            VersionString = snapshot.VersionString,
            Files = [.. checks.Select(c => c.Check)],
            BaseKeys = [ContentSnapshots.MediaKey(mediaClass.Kind, locale, mediaClass.ClassKey)],
        };

        if (kept.Count + accepted.Count > max)
        {
            draft.Warnings.Add(new PlanWarning { Key = MessageKey.MediaTooMany, Message = localizer[MessageKey.MediaTooMany, max], Field = mediaClass.ClassKey, Locale = locale });
            accepted = [.. accepted.Take(Math.Max(0, max - kept.Count))];
        }

        var removed = existing.Where(e => !kept.Contains(e)).ToList();
        var orderChanged = !kept.Select(k => k.Id).SequenceEqual(existing.Where(kept.Contains).Select(e => e.Id));

        draft.Items.AddRange(accepted.Select(a => new PlanItem { Group = ContentGroups.Media, Locale = locale, Field = mediaClass.ClassKey, After = a.FileName, Action = ChangeAction.Create }));
        draft.Items.AddRange(removed.Select(r => new PlanItem { Group = ContentGroups.Media, Locale = locale, Field = mediaClass.ClassKey, Before = r.FileName ?? r.Id, Action = ChangeAction.Delete }));

        if (orderChanged)
        {
            draft.Items.Add(new PlanItem
            {
                Group = ContentGroups.Media,
                Locale = locale,
                Field = $"{mediaClass.ClassKey}.order",
                Before = string.Join(", ", existing.Where(kept.Contains).Select(e => e.FileName ?? e.Id)),
                After = string.Join(", ", kept.Select(e => e.FileName ?? e.Id)),
                Action = ChangeAction.Update,
            });
        }

        if (draft.Items.Count == 0)
            return draft;

        draft.Changes = new ContentChangeSet
        {
            VersionId = snapshot.VersionId,
            Media =
            [
                new MediaSetChange
                {
                    Kind = mediaClass.Kind,
                    Locale = locale,
                    ClassKey = mediaClass.ClassKey,
                    PreviewFrameTimeCode = previewFrameTimeCode,
                    Items =
                    [
                        .. kept.Select(k => new MediaItemChange { ExistingId = k.Id, FileName = k.FileName, Md5 = store == StoreKind.Apple ? k.Checksum : null, Sha256 = store == StoreKind.Google ? k.Checksum : null }),
                        .. accepted.Select(a => new MediaItemChange { AssetId = a.AssetId, ObjectKey = a.ObjectKey, FileName = a.FileName, Size = a.Size, ContentType = a.ContentType, Md5 = a.Md5, Sha256 = a.Sha256 }),
                    ],
                },
            ],
        };

        draft.Calls = store == StoreKind.Apple
            ? [
                .. (set == null ? new[] { new PlanCall { Method = "POST", Path = mediaClass.Kind == MediaKind.Preview ? "/v1/appPreviewSets" : "/v1/appScreenshotSets", Target = mediaClass.ClassKey } } : Array.Empty<PlanCall>()),
                .. removed.Select(r => new PlanCall { Method = "DELETE", Path = mediaClass.Kind == MediaKind.Preview ? "/v1/appPreviews" : "/v1/appScreenshots", Target = r.FileName ?? r.Id }),
                .. accepted.SelectMany(a => new[]
                {
                    new PlanCall { Method = "POST", Path = mediaClass.Kind == MediaKind.Preview ? "/v1/appPreviews" : "/v1/appScreenshots", Target = $"{a.FileName} (reserve)" },
                    new PlanCall { Method = "PUT", Path = "uploadOperations", Target = $"{a.FileName} ({a.Size} bytes)" },
                    new PlanCall { Method = "PATCH", Path = mediaClass.Kind == MediaKind.Preview ? "/v1/appPreviews" : "/v1/appScreenshots", Target = $"{a.FileName} (commit, MD5 {a.Md5})" },
                }),
                new PlanCall { Method = "PATCH", Path = "relationships (order)", Target = mediaClass.ClassKey },
              ]
            : [
                new PlanCall { Method = "POST", Path = "edits.insert", Target = snapshot.ExternalId },
                new PlanCall { Method = "DELETE", Path = "edits.images.deleteall", Target = $"{locale} {mediaClass.ClassKey}" },
                .. kept.Concat<object>(accepted).Select(_ => new PlanCall { Method = "POST", Path = "edits.images.upload", Target = $"{locale} {mediaClass.ClassKey}" }),
                new PlanCall { Method = "POST", Path = "edits.commit", Target = snapshot.ExternalId },
              ];

        return draft;
    }

    /// <summary>
    /// Checks one file against the class's catalog rule and the set's checksums.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="mediaClass"></param>
    /// <param name="file"></param>
    /// <param name="existingChecksums"></param>
    /// <param name="catalog"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static PlanFile Check(StoreKind store, MediaClass mediaClass, InspectedAsset file, IEnumerable<string> existingChecksums, RuleCatalog catalog, IMilvaLocalizer localizer)
    {
        var result = new PlanFile { AssetId = file.AssetId, FileName = file.FileName, Width = file.Image?.Width, Height = file.Image?.Height, Md5 = file.Md5, Result = Accepted };
        var checksum = store == StoreKind.Apple ? file.Md5 : file.Sha256;

        if (existingChecksums.Any(c => string.Equals(c, checksum, StringComparison.OrdinalIgnoreCase)))
        {
            result.Result = Skipped;
            result.Reason = localizer[MessageKey.MediaAlreadyInSet];
            return result;
        }

        if (mediaClass.Kind == MediaKind.Preview)
        {
            if (file.ContentType == null || !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                return Reject(result, localizer[MessageKey.MediaNotVideo]);

            return result;
        }

        var reason = ImageProblem(mediaClass, file, catalog, localizer);

        if (reason == null)
            return result;

        result.SuggestedClassKey = file.Image == null ? null : MediaClasses.Of(store, mediaClass.Kind)
                                                                           .Where(c => c.ClassKey != mediaClass.ClassKey)
                                                                           .FirstOrDefault(c => ImageProblem(c, file, catalog, localizer) == null)?.ClassKey;

        return Reject(result, reason);
    }

    private static string ImageProblem(MediaClass mediaClass, InspectedAsset file, RuleCatalog catalog, IMilvaLocalizer localizer)
    {
        var image = file.Image;

        if (image == null)
            return localizer[MessageKey.MediaNotImage];

        var parameters = catalog.Find(mediaClass.RuleId)?.Params;

        if (parameters == null)
            return null;

        if (parameters.AlphaAllowed == false && image.HasAlpha)
            return localizer[MessageKey.MediaHasAlpha];

        if (parameters.Formats?.Count > 0 && !parameters.Formats.Any(f => FormatMatches(f, image.Format)))
            return localizer[MessageKey.MediaFormat, image.Format, string.Join(", ", parameters.Formats)];

        if (parameters.Sizes?.Count > 0 && !parameters.Sizes.Any(s => s.Width == image.Width && s.Height == image.Height))
            return localizer[MessageKey.MediaSize, $"{image.Width}x{image.Height}", string.Join(", ", parameters.Sizes.Select(s => $"{s.Width}x{s.Height}"))];

        var shortSide = Math.Min(image.Width, image.Height);
        var longSide = Math.Max(image.Width, image.Height);

        if ((parameters.MinDimension is { } min && shortSide < min) || (parameters.MaxDimension is { } maxDimension && longSide > maxDimension))
            return localizer[MessageKey.MediaSize, $"{image.Width}x{image.Height}", $"{parameters.MinDimension}-{parameters.MaxDimension}"];

        if (parameters.MaxAspectRatio is { } ratio && shortSide > 0 && longSide / (double)shortSide > ratio)
            return localizer[MessageKey.MediaAspect, ratio];

        if (parameters.MaxFileSizeBytes is { } maxBytes && file.Size > maxBytes)
            return localizer[MessageKey.FileSizeMustLowerThan, maxBytes];

        return null;
    }

    /// <summary>
    /// Whether a catalog format (jpeg, jpg, png, png-24, png-32) accepts an image of <paramref name="format"/>.
    /// </summary>
    private static bool FormatMatches(string catalogFormat, string format)
    {
        var normalized = catalogFormat.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? "jpeg" : catalogFormat;

        return normalized.Equals(format, StringComparison.OrdinalIgnoreCase) || normalized.StartsWith(format + "-", StringComparison.OrdinalIgnoreCase);
    }

    private static PlanFile Reject(PlanFile file, string reason)
    {
        file.Result = Rejected;
        file.Reason = reason;

        return file;
    }
}
