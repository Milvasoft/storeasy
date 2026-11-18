using Milvasoft.Attributes.Annotations;
using Milvasoft.Core.EntityBases.Concrete;

namespace Storeasy.Application.Dtos;

/// <summary>
/// App pool base dto for attribute usage.
/// </summary>
public class StoreasyBaseDto<TKey> : BaseDto<TKey> where TKey : struct, IEquatable<TKey>
{
    /// <inheritdoc/>
    [Pinned]
    public new TKey Id { get; set; }
}
