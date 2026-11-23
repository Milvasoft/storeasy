using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetTokenReminderList;

/// <summary>
/// Handles <see cref="GetTokenReminderListQuery"/>.
/// </summary>
/// <param name="reminderRepository"></param>
/// <param name="timeProvider"></param>
public class GetTokenReminderListQueryHandler(IStoreasyRepositoryBase<RegistryTokenReminder> reminderRepository, TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetTokenReminderListQuery, List<TokenReminderDto>>
{
    private readonly IStoreasyRepositoryBase<RegistryTokenReminder> _reminderRepository = reminderRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<TokenReminderDto>>> Handle(GetTokenReminderListQuery request, CancellationToken cancellationToken)
    {
        var reminders = await _reminderRepository.GetAllAsync(r => r.OrganizationId == request.OrganizationId, projection: TokenReminderDto.Projection, cancellationToken: cancellationToken);
        var today = _timeProvider.GetUtcNow().UtcDateTime;

        return Response<List<TokenReminderDto>>.Success([.. reminders.Select(r => r.Complete(today)).OrderBy(r => r.ExpiryDate).ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase)]);
    }
}
