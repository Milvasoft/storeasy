using Storeasy.Domain.Intake;
using System.Linq.Expressions;

namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A package upload session in lists (design 13.13).
/// </summary>
public class ArtifactSessionListDto
{
    /// <summary>
    /// Session id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Platform.
    /// </summary>
    public ArtifactPlatform Platform { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// SHA-256 of the file.
    /// </summary>
    public string Sha256 { get; set; }

    /// <summary>
    /// Where it came from.
    /// </summary>
    public ArtifactSource Source { get; set; }

    /// <summary>
    /// CI details.
    /// </summary>
    public ArtifactSourceRef SourceRef { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ArtifactSessionStatus Status { get; set; }

    /// <summary>
    /// One line state.
    /// </summary>
    public string StatusDescription { get; set; }

    /// <summary>
    /// UTC start.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// UTC end.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Store job that processes the package.
    /// </summary>
    public long? StoreJobId { get; set; }

    /// <summary>
    /// First session of the same file (duplicates).
    /// </summary>
    public long? OriginalSessionId { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    public static Expression<Func<ArtifactSession, ArtifactSessionListDto>> Projection { get; } = s => new ArtifactSessionListDto
    {
        Id = s.Id,
        AppId = s.AppId,
        AppName = s.App.Name,
        Platform = s.Platform,
        FileName = s.FileName,
        Size = s.Size,
        Sha256 = s.Sha256,
        Source = s.Source,
        SourceRef = s.SourceRef,
        Status = s.Status,
        StatusDescription = s.StatusDescription,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        StoreJobId = s.StoreJobId,
        OriginalSessionId = s.OriginalSessionId,
    };
}
