using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Developer service. Every operation runs only when <c>MILVA_ENV</c> is exactly <c>dev</c>.
/// </summary>
public interface IDeveloperService : IInterceptable
{
    /// <summary>
    /// Remove, recreates and seed database for development purposes.
    /// </summary>
    /// <returns></returns>
    Task<Response> ResetDatabaseAsync();

    /// <summary>
    /// Seeds data for development purposes.
    /// </summary>
    /// <returns></returns>
    Task<Response> SeedDevelopmentDataAsync();

    /// <summary>
    /// Seeds fake data.
    /// </summary>
    /// <param name="sameData"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    Task<Response> SeedFakeDataAsync(bool sameData = true, string locale = "tr");

    /// <summary>
    /// Initial migration operation. Also requires the configured bootstrap init key in the request's
    /// <c>X-Storeasy-Init-Key</c> header and an uninitialized database; returns the generated root password once.
    /// </summary>
    /// <returns></returns>
    Task<Response<string>> InitDatabaseAsync();

    /// <summary>
    /// Exports existing data to a JSON file.
    /// </summary>
    /// <returns></returns>
    Task<Response> ExportExistingDataAsync();

    /// <summary>
    /// Imports existing data.
    /// </summary>
    /// <returns></returns>
    Task<Response> ImportExistingDataAsync();
}
