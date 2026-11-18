using Storeasy.Application.Interfaces;
using Storeasy.Domain.Studio;
using System.Text.Json;

namespace Storeasy.Infrastructure.Studio;

/// <summary>
/// <see cref="IStarterTemplateCatalog"/> over the starter designs embedded in this assembly
/// (<c>Studio/StarterTemplates/*.json</c>, resource names <c>studio/starters/*</c>). Every starter is validated with
/// <see cref="MockupSpecRules.Validate"/> when the catalog loads, so a broken starter stops the first use loudly.
/// </summary>
/// <param name="starters">Starters in display order.</param>
public sealed class StarterTemplateCatalog(IReadOnlyList<StarterTemplate> starters) : IStarterTemplateCatalog
{
    private const string _resourcePrefix = "studio/starters/";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<StarterTemplate> _starters = starters;

    /// <inheritdoc/>
    public IReadOnlyList<StarterTemplate> All => _starters;

    /// <inheritdoc/>
    public StarterTemplate Find(string key) => _starters.FirstOrDefault(s => s.Key == key);

    /// <summary>
    /// Loads the embedded starters, ordered by resource name.
    /// </summary>
    /// <returns></returns>
    public static StarterTemplateCatalog LoadEmbedded()
    {
        var assembly = typeof(StarterTemplateCatalog).Assembly;
        var starters = new List<StarterTemplate>();

        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(_resourcePrefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name);

            var starter = JsonSerializer.Deserialize<StarterTemplate>(stream, _json) ?? throw new InvalidOperationException($"Starter template '{name}' is empty.");
            var errors = MockupSpecRules.Validate(starter.Spec);

            if (errors.Count > 0)
                throw new InvalidOperationException($"Starter template '{name}' is invalid: {string.Join("; ", errors)}");

            starters.Add(starter);
        }

        return new StarterTemplateCatalog(starters);
    }
}
