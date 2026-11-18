using MediatR;
using Milvasoft.Core.Exceptions;
using System.Collections.Concurrent;
using System.Reflection;

namespace Storeasy.Application.Behaviours;

/// <summary>
/// Checks <see cref="VerifiedEmailRequiredAttribute"/> before the handler runs. The flag is read from the database on
/// each such request (one primary key read; these requests are rare writes), so a verification takes effect at once.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
/// <param name="currentUser"></param>
/// <param name="userRepository"></param>
public sealed class VerifiedEmailBehavior<TRequest, TResponse>(ICurrentUser currentUser, IStoreasyRepositoryBase<User> userRepository) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly ConcurrentDictionary<Type, bool> _requiredCache = new();

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IStoreasyRepositoryBase<User> _userRepository = userRepository;

    /// <inheritdoc/>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var required = _requiredCache.GetOrAdd(typeof(TRequest), type => type.GetCustomAttribute<VerifiedEmailRequiredAttribute>(inherit: true) != null);

        if (!required)
            return await next(cancellationToken);

        var userId = _currentUser.GetRequiredId();

        var allowed = await _userRepository.GetFirstOrDefaultAsync(u => u.Id == userId,
                                                                   projection: u => (bool?)(u.EmailConfirmed || u.UserType == UserType.Manager),
                                                                   cancellationToken: cancellationToken);

        if (allowed != true)
            throw new MilvaUserFriendlyException(MessageKey.EmailNotVerified);

        return await next(cancellationToken);
    }
}
