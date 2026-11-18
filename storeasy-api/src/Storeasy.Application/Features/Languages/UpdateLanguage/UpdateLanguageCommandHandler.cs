using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.MultiLanguage.EntityBases.Abstract;
using Milvasoft.Core.MultiLanguage.Manager;
using Milvasoft.Interception.Ef.Transaction;
using Milvasoft.Interception.Interceptors.Logging;
using Milvasoft.Types.Structs;

namespace Storeasy.Application.Features.Languages.UpdateLanguage;

/// <summary>
/// Handles the update of the language.
/// </summary>
/// <param name="LanguageRepository"></param>
[Log]
[Transaction]
[UserActivityTrack(UserActivity.UpdateLanguages)]
public record UpdateLanguageCommandHandler(IStoreasyRepositoryBase<Language> LanguageRepository) : IInterceptable, ICommandHandler<UpdateLanguageCommand, int>
{
    private readonly IStoreasyRepositoryBase<Language> _languageRepository = LanguageRepository;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateLanguageCommand request, CancellationToken cancellationToken)
    {
        var current = await _languageRepository.GetFirstOrDefaultAsync(l => l.Id == request.Id, projection: l => new Language { Id = l.Id, IsDefault = l.IsDefault, Supported = l.Supported }, cancellationToken: cancellationToken);

        if (current == null)
            return Response<int>.Error(0, MessageKey.PleaseSendCorrect);

        var willBeDefault = request.IsDefault.IsUpdated && request.IsDefault.Value ? true : current.IsDefault;
        var willBeSupported = request.Supported.IsUpdated ? request.Supported.Value : current.Supported;

        // The default language is always supported: it cannot be turned off, and an unsupported language cannot become it.
        if (current.IsDefault && !willBeSupported)
            return Response<int>.Error(0, MessageKey.DefaultLanguageRequired);

        if (willBeDefault && !willBeSupported)
            return Response<int>.Error(0, MessageKey.UnsupportedLanguageCannotBeDefault);

        if (request.IsDefault.IsUpdated)
        {
            if (request.IsDefault.Value)
            {
                var languages = await _languageRepository.GetAllAsync(l => l.IsDefault, cancellationToken: cancellationToken);

                foreach (var language in languages)
                {
                    language.IsDefault = false;
                }

                await _languageRepository.BulkUpdateAsync(languages, null, cancellationToken);
            }
            else
                request.IsDefault = new UpdateProperty<bool>();
        }

        var setPropertyBuilder = _languageRepository.GetUpdatablePropertiesBuilder(request);

        // A request that only carried IsDefault=false leaves nothing to set (an empty ExecuteUpdate is invalid SQL).
        if (setPropertyBuilder.SetPropertyCallsLog.Count > 0)
            await _languageRepository.ExecuteUpdateAsync(request.Id, setPropertyBuilder, cancellationToken: cancellationToken);

        var latestLanguages = await _languageRepository.GetAllAsync(cancellationToken: cancellationToken);

        var languageSeed = latestLanguages.Cast<ILanguage>().ToList();

        MultiLanguageManager.UpdateLanguagesList(languageSeed);

        return Response<int>.Success(request.Id);
    }
}
