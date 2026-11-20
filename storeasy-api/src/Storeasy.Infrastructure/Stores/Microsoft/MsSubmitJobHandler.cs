using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Enums;
using Storeasy.Domain.MsStore;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// The <see cref="StoreJobKind.MsSubmit"/> job (queued only after the typed confirmation and
/// <see cref="OrgPermission.SubmitForReview"/>). MSIX: reads the draft again, refuses when its content is no longer what
/// storeasy wrote (changed in Partner Center), commits it and polls the status until the commit is taken in. MSI/EXE:
/// requires a ready draft and signed installers, submits and reads the publishing status. Runs once (no retry): a commit
/// is not repeated blindly.
/// </summary>
/// <param name="productRepository"></param>
/// <param name="submissionRepository"></param>
/// <param name="packageRepository"></param>
/// <param name="microsoftStoreClient"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class MsSubmitJobHandler(IStoreasyRepositoryBase<MsProduct> productRepository,
                                       IStoreasyRepositoryBase<MsSubmission> submissionRepository,
                                       IStoreasyRepositoryBase<MsInstallerPackage> packageRepository,
                                       IMicrosoftStoreClient microsoftStoreClient,
                                       MicrosoftStoreOptions options,
                                       TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = submissionRepository;
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _packageRepository = packageRepository;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = microsoftStoreClient;
    private readonly MicrosoftStoreOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.MsSubmit;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<MsSubmitPayload>() ?? throw new StoreJobException("The job has no submission.", transient: false);
        var product = await _productRepository.GetMsProductAsync(context.OrganizationId, payload.StoreAppId, cancellationToken) ?? throw new StoreJobException("The Microsoft Store product was removed.", transient: false);
        var connection = (product.ConnectionId.HasValue ? await context.OpenConnectionAsync(product.ConnectionId.Value, cancellationToken) : null)
                         ?? throw new StoreJobException("The store connection was removed.", transient: false);

        return product.Kind == MsProductKind.MsiExe
            ? await SubmitProductAsync(context, connection.SecretJson, product, cancellationToken)
            : await CommitAsync(context, connection.SecretJson, product, payload, cancellationToken);
    }

    private async Task<StoreJobOutcome> CommitAsync(IStoreJobContext context, string secret, MsProductContext product, MsSubmitPayload payload, CancellationToken cancellationToken)
    {
        var submission = MsJobSupport.Ref(payload.Target, product.ProductId, payload.FlightId, payload.AddonStoreId, payload.SubmissionId);
        var row = await _submissionRepository.GetFirstOrDefaultAsync(s => s.StoreAppId == product.StoreAppId && s.Target == payload.Target && s.ExternalId == payload.SubmissionId,
                                                                     projection: s => new MsSubmission { Id = s.Id, ContentHash = s.ContentHash, CreatedByStoreasy = s.CreatedByStoreasy },
                                                                     cancellationToken: cancellationToken);

        if (row == null || !row.CreatedByStoreasy)
            throw new StoreJobException("The draft was not created by storeasy.", transient: false);

        // 1. The draft must still be what storeasy wrote.
        var checkStep = await context.BeginStepAsync("submission.check", StepDirection.Outbound, payload.SubmissionId, cancellationToken: cancellationToken);
        var document = await _microsoftStoreClient.GetSubmissionAsync(secret, submission, cancellationToken);
        var status = MsSubmissionJson.ParseStatus(MsSubmissionJson.String(document, "status"));

        if (status != MsSubmissionStatus.PendingCommit)
            throw new StoreJobException($"The submission is {status}, not a draft.", transient: false);

        if (row.ContentHash != null && row.ContentHash != MsSubmissionJson.ContentHash(document))
        {
            await _submissionRepository.MarkModifiedOutsideApiAsync(product.StoreAppId, payload.Target, payload.SubmissionId, CancellationToken.None);

            throw new StoreJobException("The draft was changed in Partner Center; the API cannot commit it anymore. Delete it and create a new one.", transient: false);
        }

        await context.EndStepAsync(checkStep, StoreJobStepStatus.Succeeded, null, cancellationToken);

        // 2. Commit.
        var commitStep = await context.BeginStepAsync("submission.commit", StepDirection.Outbound, payload.SubmissionId, cancellationToken: cancellationToken);
        MsStatusRecord result;

        try
        {
            result = await _microsoftStoreClient.CommitSubmissionAsync(secret, submission, cancellationToken);
        }
        catch (StoreCallException ex) when (ex.StatusCode == 409)
        {
            await _submissionRepository.MarkModifiedOutsideApiAsync(product.StoreAppId, payload.Target, payload.SubmissionId, CancellationToken.None);

            throw new StoreJobException($"Microsoft refused the commit (409): {ex.Message}", transient: false, ex);
        }

        await context.EndStepAsync(commitStep, StoreJobStepStatus.Succeeded, result.Status.ToString(), cancellationToken);

        // 3. Poll until the commit is taken in (PreProcessing) or failed; the sync follows certification.
        var pollStep = await context.BeginStepAsync("submission.status", StepDirection.Inbound, cancellationToken: cancellationToken);

        for (var attempt = 0; attempt < Math.Max(1, _options.StatusPollAttempts) && result.Status is MsSubmissionStatus.CommitStarted or MsSubmissionStatus.PendingCommit; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(_options.StatusPollIntervalMilliseconds), _timeProvider, cancellationToken);

            result = await _microsoftStoreClient.GetSubmissionStatusAsync(secret, submission, cancellationToken);
        }

        await SaveStatusAsync(context.OrganizationId, product.StoreAppId, payload.Target, payload.SubmissionId, result.Status, result.Details, document, cancellationToken);

        var failed = result.Status is MsSubmissionStatus.CommitFailed or MsSubmissionStatus.PreProcessingFailed or MsSubmissionStatus.CertificationFailed;

        await context.EndStepAsync(pollStep, failed ? StoreJobStepStatus.Failed : StoreJobStepStatus.Succeeded, $"{result.Status} {string.Join("; ", result.Details.Errors.Select(e => $"{e.Code} {e.Details}"))}".Trim(), cancellationToken);

        if (failed)
            throw new StoreJobException($"The submission failed: {result.Status}.", transient: false);

        return new StoreJobOutcome($"Submission {payload.SubmissionId} committed: {result.Status}.", new { payload.SubmissionId, result.Status });
    }

    private async Task<StoreJobOutcome> SubmitProductAsync(IStoreJobContext context, string secret, MsProductContext product, CancellationToken cancellationToken)
    {
        var checkStep = await context.BeginStepAsync("product.check", StepDirection.Outbound, product.ProductId, cancellationToken: cancellationToken);
        var status = await _microsoftStoreClient.GetProductStatusAsync(secret, product.ProductId, cancellationToken);

        if (!status.IsReady)
            throw new StoreJobException($"The product is not ready to submit: {string.Join("; ", status.Errors.Select(e => $"{e.Code} {e.Details}"))}".Trim(), transient: false);

        var packages = await _packageRepository.GetAllAsync(p => p.StoreAppId == product.StoreAppId, projection: p => new MsInstallerPackage { Id = p.Id, SignatureStatus = p.SignatureStatus }, cancellationToken: cancellationToken);

        if (packages.Count == 0 || packages.Any(p => p.SignatureStatus != MsSignatureStatus.Signed))
            throw new StoreJobException("Every installer must be Authenticode signed before submitting.", transient: false);

        await context.EndStepAsync(checkStep, StoreJobStepStatus.Succeeded, null, cancellationToken);

        var submitStep = await context.BeginStepAsync("product.submit", StepDirection.Outbound, cancellationToken: cancellationToken);
        var submissionId = await _microsoftStoreClient.SubmitProductAsync(secret, product.ProductId, cancellationToken);
        var publishing = await _microsoftStoreClient.GetProductSubmissionStatusAsync(secret, product.ProductId, submissionId, cancellationToken);
        var state = publishing.HasFailed ? MsSubmissionStatus.Failed : publishing.Status;

        await SaveStatusAsync(context.OrganizationId, product.StoreAppId, MsSubmissionTarget.Installer, submissionId, state, null, null, cancellationToken);

        await _productRepository.ExecuteUpdateAsync(p => p.StoreAppId == product.StoreAppId,
                                                    new SetPropertyBuilder<MsProduct>().SetPropertyValue(p => p.PendingSubmissionId, state == MsSubmissionStatus.InProgress ? submissionId : null),
                                                    cancellationToken);

        await context.EndStepAsync(submitStep, publishing.HasFailed ? StoreJobStepStatus.Failed : StoreJobStepStatus.Succeeded, $"{submissionId}: {state}", cancellationToken);

        if (publishing.HasFailed)
            throw new StoreJobException($"Submission {submissionId} failed.", transient: false);

        return new StoreJobOutcome($"Product {product.ProductId} submitted ({submissionId}): {state}.", new { SubmissionId = submissionId, Status = state });
    }

    /// <summary>
    /// Saves the status of a submission storeasy committed or submitted; <paramref name="document"/> is the committed MSIX
    /// document (its publish mode is stored), null for MSI/EXE submissions.
    /// </summary>
    private Task SaveStatusAsync(int organizationId,
                                 int storeAppId,
                                 MsSubmissionTarget target,
                                 string submissionId,
                                 MsSubmissionStatus status,
                                 Domain.JsonModels.MsStatusDetails details,
                                 System.Text.Json.Nodes.JsonObject document,
                                 CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return _submissionRepository.UpsertSubmissionAsync(organizationId, storeAppId, target, submissionId, row =>
        {
            MsRowWriter.Observe(row, status, details, now);

            row.IsPending = status is not (MsSubmissionStatus.Published or MsSubmissionStatus.Canceled);
            row.CreatedByStoreasy = row.CreatedByStoreasy || target == MsSubmissionTarget.Installer;

            if (document != null)
            {
                row.PublishMode = MsSubmissionJson.ReadPublishMode(document);
                row.PublishDate = MsSubmissionJson.ReadPublishDate(document);
            }

            row.SyncedDate = now;
        }, cancellationToken);
    }
}
