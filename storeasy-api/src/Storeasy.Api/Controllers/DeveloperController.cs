using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.PermissionManager;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Developer endpoints. They run only when <c>MILVA_ENV</c> is exactly <c>dev</c> (404 in <c>test</c>, <c>prod</c> and any
/// other value). Every endpoint requires a super admin, except <c>database/init</c>, which is anonymous and guarded by the
/// bootstrap init key instead (see <see cref="InitDatabaseAsync"/>).
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
[Auth(PermissionCatalog.App.SuperAdmin)]
public class DeveloperController(IDeveloperService developerService) : ControllerBase
{
    private readonly IDeveloperService _developerService = developerService;

    /// <summary>
    /// Remove, recreates and seed database for development purposes.
    /// </summary>
    /// <returns></returns>
    [HttpPost("database/reset")]
    public Task<Response> MigrateAsync() => _developerService.ResetDatabaseAsync();

    /// <summary>
    /// Seeds the development users and role (<c>admin</c>, <c>viewer</c>) into a database initialized with
    /// <c>database/init</c>; call it with the root user's token ("Already seeded!" the second time).
    /// </summary>
    /// <returns></returns>
    [HttpPost("database/seed")]
    public Task<Response> SeedDataAsync() => _developerService.SeedDevelopmentDataAsync();

    /// <summary>
    /// Seeds fake data for development purposes.
    /// </summary>
    /// <returns></returns>
    [HttpPost("database/seed/fake")]
    public Task<Response> SeedFakeDataAsync(bool sameData = true, string locale = "tr") => _developerService.SeedFakeDataAsync(sameData, locale);

    /// <summary>
    /// Initial migration operation; returns the generated root password once. Anonymous, and runs only when
    /// <c>MILVA_ENV</c> is <c>dev</c>, <c>Storeasy:Bootstrap:InitKey</c> is configured (at least 32 characters; otherwise
    /// 404), the request carries the same value in the <c>X-Storeasy-Init-Key</c> header (otherwise 401) and the database
    /// is not initialized yet ("Already initialized!" otherwise). The header is never logged.
    /// </summary>
    /// <returns></returns>
    [HttpPost("database/init")]
    [AllowAnonymous]
    [RateLimit(5, 60, "developer-database-init")]
    public async Task<Response> InitDatabaseAsync() => await _developerService.InitDatabaseAsync();

    /// <summary>
    /// Exports product related data.
    /// </summary>
    /// <returns></returns>
    [HttpGet("export/productRelatedData")]
    public Task<Response> ExportExistingDataAsync() => _developerService.ExportExistingDataAsync();

    /// <summary>
    /// Imports exported product related data. 
    /// </summary>
    /// <returns></returns>
    [HttpGet("import/productRelatedData")]
    public Task<Response> ImportExistingDataAsync() => _developerService.ImportExistingDataAsync();
}