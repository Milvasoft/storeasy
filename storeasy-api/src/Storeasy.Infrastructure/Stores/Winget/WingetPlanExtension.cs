using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Winget;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Winget;

/// <summary>
/// Applies an approved winget plan (<see cref="ChangePlanKind.WingetChange"/>): downloads the installers and puts their
/// SHA-256 into the installer manifest (a hash given in the plan must match), checks the manifests once more, pushes a
/// branch to the owner's fork and opens the pull request to winget-pkgs. The pull request is then tracked by the sync;
/// its merge is the publication. When GitHub refuses the pull request itself the job fails with the compare page of the
/// pushed branch, so the owner can open it by hand.
/// </summary>
/// <param name="client"></param>
/// <param name="contentService"></param>
/// <param name="pullRequestRepository"></param>
/// <param name="timeProvider"></param>
public sealed class WingetPlanExtension(IWingetClient client,
                                        IStoreContentService contentService,
                                        IStoreasyRepositoryBase<WingetPullRequest> pullRequestRepository,
                                        TimeProvider timeProvider) : IChangePlanExtension
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IWingetClient _client = client;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreasyRepositoryBase<WingetPullRequest> _pullRequestRepository = pullRequestRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public bool Handles(ChangePlanKind kind) => kind == ChangePlanKind.WingetChange;

    /// <inheritdoc/>
    public Task<StoreContentRead> ReadBaseAsync(StoreAppContext app, IReadOnlyCollection<string> baseKeys, CancellationToken cancellationToken)
    {
        var version = baseKeys.FirstOrDefault(k => k.StartsWith(WingetContent.ReadPrefix, StringComparison.Ordinal))?[WingetContent.ReadPrefix.Length..];

        return _contentService.ReadAsync(app, ContentParts.All, version, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(ChangePlanApplyRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var change = JsonSerializer.Deserialize<WingetPlannedChange>(request.Changes, _json) ?? throw new StoreJobException("The plan has no winget change.", transient: false);
        var files = change.Files.ToList();
        var lines = new List<string>();

        if (change.Installers.Count > 0)
        {
            var step = await context.BeginStepAsync("installers.hash", StepDirection.Outbound, total: change.Installers.Count, cancellationToken: cancellationToken);
            var hashes = new Dictionary<int, string>();

            for (var i = 0; i < change.Installers.Count; i++)
            {
                var installer = change.Installers[i];
                var hash = await _client.HashInstallerAsync(installer.Url, cancellationToken);

                if (installer.ExpectedSha256 != null && !string.Equals(installer.ExpectedSha256, hash.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    await context.EndStepAsync(step, StoreJobStepStatus.Failed, $"SHA256 of {installer.Url} does not match.", cancellationToken);

                    throw new StoreJobException($"The download of {installer.Url} has SHA256 {hash.Sha256}, not {installer.ExpectedSha256}; nothing was pushed.", transient: false);
                }

                hashes[installer.Index] = hash.Sha256;
                await context.ReportProgressAsync(step, i + 1, cancellationToken);
            }

            files = WithHashes(files, hashes);

            await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{hashes.Count} installers hashed.", cancellationToken);
            lines.Add($"{hashes.Count} installers hashed.");
        }

        // A new version is the full manifest set and its hashes were just written: it is checked again as a whole. A text
        // edit carries only the changed locale manifests, checked with the rest of the version when it was planned.
        if (change.Kind == WingetChangeKind.NewVersion)
        {
            var set = WingetManifestSet.FromFiles(files.ToDictionary(f => f.Path[(f.Path.LastIndexOf('/') + 1)..], f => f.Content, StringComparer.Ordinal));
            var blocking = WingetManifestValidator.Validate(set).Where(i => i.Blocking).ToList();

            if (blocking.Count > 0)
                throw new StoreJobException($"The manifests do not pass the schema checks: {string.Join("; ", blocking.Select(Describe))}", transient: false);
        }

        var pullStep = await context.BeginStepAsync("pull.open", StepDirection.Outbound, change.Title, cancellationToken: cancellationToken);
        var opened = await _client.OpenPullRequestAsync(request.Target.SecretJson, new WingetPullRequestDraft(change.Branch, change.CommitMessage, change.Title, change.Body, files, change.DeletePaths), cancellationToken);

        if (opened.Number is not int number)
        {
            await context.EndStepAsync(pullStep, StoreJobStepStatus.Failed, opened.RefusedReason, cancellationToken);

            throw new StoreJobException($"The branch {change.Branch} is pushed to the fork but GitHub refused the pull request ({opened.RefusedReason}). Open it from {opened.Url}; the sync then tracks it.", transient: false);
        }

        await context.EndStepAsync(pullStep, StoreJobStepStatus.Succeeded, $"#{number}", cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var existing = await _pullRequestRepository.GetFirstOrDefaultAsync(p => p.StoreAppId == request.StoreAppId && p.Number == number && p.OrganizationId == request.OrganizationId,
                                                                               projection: p => new WingetPullRequest { Id = p.Id },
                                                                               cancellationToken: cancellationToken);

        if (existing == null)
        {
            await _pullRequestRepository.AddAsync(new WingetPullRequest
            {
                OrganizationId = request.OrganizationId,
                StoreAppId = request.StoreAppId,
                Number = number,
                Url = opened.Url,
                Title = Trim(change.Title, WingetPullRequest.TitleMaxLength),
                HeadBranch = Trim(change.Branch, WingetPullRequest.BranchMaxLength),
                HeadSha = opened.HeadSha,
                PackageVersion = change.PackageVersion,
                ChangeKind = change.Kind,
                State = WingetPullRequestState.Validating,
                IsOpen = true,
                CreatedByStoreasy = true,
                PlanId = request.PlanId,
                OpenedDate = now,
                SyncedDate = now,
                StatusHistory = [new WingetStateChange { State = WingetPullRequestState.Validating, Date = now }],
            }, cancellationToken);
        }

        lines.Add($"Pull request #{number.ToString(CultureInfo.InvariantCulture)} opened: {opened.Url}");

        return lines;
    }

    /// <inheritdoc/>
    public AuditAction AuditActionOf(ChangePlanKind kind, PlanItem item) => AuditAction.WingetPullRequestOpened;

    /// <summary>
    /// The files with the computed hashes written into the installer manifest's entries.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="hashes">Installer index to SHA-256.</param>
    /// <returns></returns>
    public static List<WingetManifestFile> WithHashes(List<WingetManifestFile> files, IReadOnlyDictionary<int, string> hashes)
    {
        return [.. files.Select(file =>
        {
            if (!file.Path.EndsWith(".installer.yaml", StringComparison.Ordinal))
                return file;

            var manifest = WingetYaml.Parse(file.Content);
            var installers = manifest.Seq("Installers")?.OfType<YamlMap>().ToList() ?? [];

            foreach (var (index, sha) in hashes)
            {
                if (index < installers.Count)
                    installers[index].Set("InstallerSha256", sha);
            }

            return file with { Content = WingetManifestSet.Render(manifest, WingetManifestSet.InstallerType) };
        })];
    }

    private static string Describe(WingetIssue issue) => $"{issue.Key} {string.Join(", ", issue.Args.Select(a => Convert.ToString(a, CultureInfo.InvariantCulture)))}".Trim();

    private static string Trim(string value, int max) => value?.Length > max ? value[..max] : value;
}
