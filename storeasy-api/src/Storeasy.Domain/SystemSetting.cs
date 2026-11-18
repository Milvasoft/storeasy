using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain;

/// <summary>
/// An instance setting a platform manager changed at runtime; it wins over the same setting in the configuration
/// (<see cref="SystemSettingKeys"/>). A missing row means the configuration applies.
/// </summary>
[Table(TableNames.SystemSettings)]
[Index(nameof(Key), IsUnique = true)]
public class SystemSetting : AuditableEntity<int>
{
    /// <summary>
    /// Setting key, one of <see cref="SystemSettingKeys"/>.
    /// </summary>
    [MaxLength(100)]
    public string Key { get; set; }

    /// <summary>
    /// Value as text.
    /// </summary>
    [MaxLength(1000)]
    public string Value { get; set; }
}

/// <summary>
/// Keys of <see cref="SystemSetting"/> rows.
/// </summary>
public static class SystemSettingKeys
{
    /// <summary>
    /// Who may register (a <see cref="Enums.RegistrationMode"/> name); overrides <c>Storeasy:Registration:Mode</c>.
    /// </summary>
    public const string RegistrationMode = "Registration.Mode";
}
