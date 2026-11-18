using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Stores;

/// <summary>
/// One step of a <see cref="StoreJob"/> (e.g. <c>apps.list</c>, <c>builds.save</c>): the live activity view draws it and
/// the job detail replays it. Rows of every attempt are kept, so a retried job shows what failed before.
/// </summary>
[Table(TableNames.StoreJobSteps)]
[Index(nameof(StoreJobId), nameof(Order))]
public class StoreJobStep : BaseEntity<long>
{
    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Column length of <see cref="Message"/>.
    /// </summary>
    public const int MessageMaxLength = 500;

    /// <summary>
    /// Owning job.
    /// </summary>
    public long StoreJobId { get; set; }

    /// <summary>
    /// Position in the job, from 1, across attempts.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Attempt the step belongs to.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Machine name of the step (<c>area.action</c>).
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Which way data moves.
    /// </summary>
    public StepDirection Direction { get; set; }

    /// <summary>
    /// Current state.
    /// </summary>
    public StoreJobStepStatus Status { get; set; } = StoreJobStepStatus.Running;

    /// <summary>
    /// Units done (bytes, items). Null when the step has no measurable progress.
    /// </summary>
    public long? Progress { get; set; }

    /// <summary>
    /// Units in total. Null when unknown.
    /// </summary>
    public long? Total { get; set; }

    /// <summary>
    /// Redacted, short human readable note.
    /// </summary>
    [MaxLength(MessageMaxLength)]
    public string Message { get; set; }

    /// <summary>
    /// UTC instant the step started.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// UTC instant the step ended.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Navigation property of the job.
    /// </summary>
    [ForeignKey(nameof(StoreJobId))]
    public virtual StoreJob StoreJob { get; set; }
}
