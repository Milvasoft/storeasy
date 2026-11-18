using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Studio;
using System.Text.Json;

namespace Storeasy.Application.Features.Mockups.CreateMockupTemplate;

/// <summary>
/// Handles <see cref="CreateMockupTemplateCommand"/>. The template, its slots and its captions are written in one
/// transaction. Starter captions go to the template locales of the same language (<c>en-US</c> takes the <c>en</c>
/// samples).
/// </summary>
/// <param name="TemplateRepository"></param>
/// <param name="SlotRepository"></param>
/// <param name="CaptionRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="StarterTemplateCatalog"></param>
/// <param name="TransactionRunner"></param>
/// <param name="AuditTrail"></param>
[Log]
public record CreateMockupTemplateCommandHandler(IStoreasyRepositoryBase<MockupTemplate> TemplateRepository,
                                                 IStoreasyRepositoryBase<MockupSlot> SlotRepository,
                                                 IStoreasyRepositoryBase<MockupCaption> CaptionRepository,
                                                 IStoreasyRepositoryBase<App> AppRepository,
                                                 IStarterTemplateCatalog StarterTemplateCatalog,
                                                 ITransactionRunner TransactionRunner,
                                                 IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<CreateMockupTemplateCommand, int>
{
    /// <summary>
    /// Starter used when the request names none.
    /// </summary>
    public const string DefaultStarterKey = "ultramarine";

    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = TemplateRepository;
    private readonly IStoreasyRepositoryBase<MockupSlot> _slotRepository = SlotRepository;
    private readonly IStoreasyRepositoryBase<MockupCaption> _captionRepository = CaptionRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStarterTemplateCatalog _starterTemplateCatalog = StarterTemplateCatalog;
    private readonly ITransactionRunner _transactionRunner = TransactionRunner;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CreateMockupTemplateCommand request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId,
                                                              projection: a => new App { Id = a.Id },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        var name = request.Name.Trim();
        var taken = await _templateRepository.GetFirstOrDefaultAsync(t => t.AppId == request.AppId && t.Name == name,
                                                                     projection: t => new MockupTemplate { Id = t.Id },
                                                                     cancellationToken: cancellationToken);

        if (taken != null)
            return Response<int>.Error(0, MessageKey.MockupTemplateNameExists);

        var locales = request.Locales.Select(l => l.Trim()).ToList();
        var template = new MockupTemplate
        {
            OrganizationId = request.OrganizationId,
            AppId = request.AppId,
            Name = name,
            Locales = locales,
            PrimaryLocale = locales.First(l => string.Equals(l, request.PrimaryLocale.Trim(), StringComparison.OrdinalIgnoreCase)),
            Version = 1,
        };

        List<MockupSlot> slots = [];
        List<MockupCaption> captions = [];

        if (request.CopyFromTemplateId is int sourceId)
        {
            var source = await _templateRepository.GetMockupSnapshotAsync(request.OrganizationId, sourceId, cancellationToken);

            if (source == null)
                return Response<int>.Error(0, MessageKey.MockupTemplateNotFound);

            template.Spec = CopySpec(MockupSpecRules.Upgrade(source.Spec));
            template.SlotCount = request.SlotCount ?? source.SlotCount;
            slots = [.. source.Slots.Where(s => s.Order <= template.SlotCount && (s.Locale == null || locales.Contains(s.Locale)))
                                    .Select(s => new MockupSlot { Order = s.Order, Family = s.Family, Locale = s.Locale, AssetId = s.AssetId })];

            if (request.CopyCaptions)
            {
                captions = [.. source.Captions.Where(c => c.SlotOrder <= template.SlotCount && locales.Contains(c.Locale))
                                              .Select(c => new MockupCaption { Locale = c.Locale, SlotOrder = c.SlotOrder, Title = c.Title, Subtitle = c.Subtitle })];
            }
        }
        else
        {
            var starter = _starterTemplateCatalog.Find(request.StarterKey ?? DefaultStarterKey);

            if (starter == null)
                return Response<int>.Error(0, MessageKey.MockupStarterNotFound);

            template.Spec = CopySpec(MockupSpecRules.Upgrade(starter.Spec));
            template.SlotCount = request.SlotCount ?? starter.SlotCount;
            template.StarterKey = starter.Key;
            captions = StarterCaptions(starter, locales, template.SlotCount);
        }

        await _transactionRunner.RunAsync(async () =>
        {
            await _templateRepository.AddAsync(template, cancellationToken);

            slots.ForEach(s => s.MockupTemplateId = template.Id);
            captions.ForEach(c => c.MockupTemplateId = template.Id);

            if (slots.Count > 0)
                await _slotRepository.AddRangeAsync(slots, cancellationToken);

            if (captions.Count > 0)
                await _captionRepository.AddRangeAsync(captions, cancellationToken);
        });

        var origin = request.CopyFromTemplateId is int copied ? $"copy of template {copied}" : $"starter {template.StarterKey}";

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MockupTemplateCreated,
                                     template.Id.ToString(),
                                     $"Mockup template '{name}' created for app {request.AppId} ({origin}).",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(template.Id);
    }

    private static List<MockupCaption> StarterCaptions(StarterTemplate starter, List<string> locales, int slotCount)
    {
        var captions = new List<MockupCaption>();

        foreach (var locale in locales)
        {
            var language = locale.Split('-')[0].ToLowerInvariant();

            if (!starter.Captions.TryGetValue(language, out var samples))
                continue;

            captions.AddRange(samples.Take(slotCount).Select((sample, index) => new MockupCaption
            {
                Locale = locale,
                SlotOrder = index + 1,
                Title = sample.Title,
                Subtitle = sample.Subtitle,
            }));
        }

        return captions;
    }

    private static MockupSpec CopySpec(MockupSpec spec) => JsonSerializer.Deserialize<MockupSpec>(JsonSerializer.Serialize(spec));
}
