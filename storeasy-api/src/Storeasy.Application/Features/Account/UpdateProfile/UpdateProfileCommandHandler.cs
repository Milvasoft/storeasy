using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.MultiLanguage.Manager;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.UpdateProfile;

/// <summary>
/// Handles <see cref="UpdateProfileCommand"/> with one conditional update of the caller's row. The language is stored in
/// the instance's spelling (tr-TR).
/// </summary>
/// <param name="UserRepository"></param>
/// <param name="CurrentUser"></param>
[Log]
public record UpdateProfileCommandHandler(IStoreasyRepositoryBase<User> UserRepository, ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<UpdateProfileCommand, int>
{
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        var language = string.IsNullOrWhiteSpace(request.Language) ? null : LanguagesSeed.Seed.First(l => string.Equals(l.Code, request.Language.Trim(), StringComparison.OrdinalIgnoreCase)).Code;

        var updated = await _userRepository.ExecuteUpdateAsync(u => u.Id == userId,
                                                               new SetPropertyBuilder<User>().SetPropertyValue(u => u.Name, request.Name.Trim())
                                                                                             .SetPropertyValue(u => u.Surname, string.IsNullOrWhiteSpace(request.Surname) ? null : request.Surname.Trim())
                                                                                             .SetPropertyValue(u => u.PreferredLanguage, language),
                                                               cancellationToken);

        return updated == 0 ? Response<int>.Error(0, MessageKey.UserNotFound) : Response<int>.Success(userId);
    }
}
