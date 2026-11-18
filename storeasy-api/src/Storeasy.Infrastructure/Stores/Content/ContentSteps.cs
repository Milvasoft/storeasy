using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// Runs one store call (or a small group of them) as a job step: the step is opened, the work runs, the step ends
/// succeeded with the work's note or failed with the (already redacted) error, which is rethrown.
/// </summary>
public static class ContentSteps
{
    /// <summary>
    /// Runs <paramref name="work"/> as step <paramref name="name"/>.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name"></param>
    /// <param name="message"></param>
    /// <param name="work">Returns the note the step ends with.</param>
    /// <param name="cancellationToken"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public static async Task<string> RunAsync(IStoreJobContext context,
                                              string name,
                                              string message,
                                              Func<Task<string>> work,
                                              CancellationToken cancellationToken,
                                              StepDirection direction = StepDirection.Outbound)
    {
        var step = await context.BeginStepAsync(name, direction, message, cancellationToken: cancellationToken);

        string note;

        try
        {
            note = await work();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await context.EndStepAsync(step, StoreJobStepStatus.Failed, ex.Message, cancellationToken);
            throw;
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, note ?? message, cancellationToken);

        return note ?? message;
    }

    /// <summary>
    /// Reads a stored object into memory (screenshots and graphics; bundles are streamed instead).
    /// </summary>
    /// <param name="fileStorage"></param>
    /// <param name="objectKey"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<byte[]> ReadObjectAsync(IFileStorage fileStorage, string objectKey, CancellationToken cancellationToken)
    {
        await using var stream = await fileStorage.OpenReadAsync(objectKey, cancellationToken);
        using var buffer = new MemoryStream();

        await stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }
}
