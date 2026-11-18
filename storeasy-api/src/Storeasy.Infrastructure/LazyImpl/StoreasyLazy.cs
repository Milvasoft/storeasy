using Microsoft.Extensions.DependencyInjection;

namespace Storeasy.Infrastructure.LazyImpl;

/// <summary>
/// Custom <see cref="Lazy{T}"/> class.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <remarks>
/// Constructor of <see cref="StoreasyLazy{T}"/>.
/// </remarks>
/// <param name="serviceProvider"></param>
public class StoreasyLazy<T>(IServiceProvider serviceProvider) : Lazy<T>(serviceProvider.GetRequiredService<T>)
{
}
