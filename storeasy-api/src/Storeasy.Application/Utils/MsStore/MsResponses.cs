using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// Responses of the Microsoft Store handlers that read the store inside the request.
/// </summary>
public static class MsResponses
{
    /// <summary>
    /// A failed store read: <see cref="MessageKey.MsStoreCallFailed"/> followed by the store's (redacted) answer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static Response<T> StoreFailure<T>(StoreCallException exception)
    {
        var response = Response<T>.Error(default, MessageKey.MsStoreCallFailed);

        response.AddMessage(SecretRedactor.RedactAndTrim(exception.Message, 500), MessageType.Warning);

        return response;
    }
}
