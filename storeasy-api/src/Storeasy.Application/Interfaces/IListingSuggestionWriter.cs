namespace Storeasy.Application.Interfaces;

/// <summary>
/// Writes a suggestion for one store listing field with the organization's own AI key (the same provider, key and model
/// as the review reply drafts). Nothing is saved or sent to a store: a member reviews the suggestion and plans it.
/// </summary>
public interface IListingSuggestionWriter
{
    /// <summary>
    /// Asks the model for a value of the field.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ListingSuggestionResult> SuggestAsync(ListingSuggestionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the model for the App Store primary and secondary category, chosen from the store's list.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<CategorySuggestionResult> SuggestCategoriesAsync(CategorySuggestionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// What the model is given for one listing field. Every text is data for the prompt, never instructions.
/// </summary>
/// <param name="ApiKey">The organization's Anthropic key.</param>
/// <param name="Store">Store of the listing.</param>
/// <param name="AppName">storeasy's name of the app.</param>
/// <param name="Field">Listing field key (name, subtitle, description, keywords, promotionalText, whatsNew, title, shortDescription, fullDescription).</param>
/// <param name="Locale">Language of the suggestion.</param>
/// <param name="PrimaryLocale">Primary language of the listing.</param>
/// <param name="MaxLength">Character limit of the field (catalog), or null.</param>
/// <param name="MaxBytes">Byte limit of the field (catalog), or null.</param>
/// <param name="MinLength">Minimum characters (catalog), or null.</param>
/// <param name="Rules">Catalog rules of the field (title and description).</param>
/// <param name="CurrentValue">The field's value in the store, or null.</param>
/// <param name="LocaleTexts">The other listing texts of the same language.</param>
/// <param name="PrimaryTexts">The listing texts of the primary language (when it is another language).</param>
/// <param name="Profile">The app's catalog profile as short lines (platforms, device families, type, features).</param>
/// <param name="VersionString">Version the listing belongs to (App Store), or null.</param>
/// <param name="Instruction">The member's own note for this suggestion, or null.</param>
/// <param name="Model">Model to use; null for the instance default.</param>
public sealed record ListingSuggestionRequest(string ApiKey,
                                              StoreKind Store,
                                              string AppName,
                                              string Field,
                                              string Locale,
                                              string PrimaryLocale,
                                              int? MaxLength,
                                              int? MaxBytes,
                                              int? MinLength,
                                              IReadOnlyList<string> Rules,
                                              string CurrentValue,
                                              IReadOnlyDictionary<string, string> LocaleTexts,
                                              IReadOnlyDictionary<string, string> PrimaryTexts,
                                              IReadOnlyList<string> Profile,
                                              string VersionString,
                                              string Instruction,
                                              string Model = null);

/// <summary>
/// Result of <see cref="IListingSuggestionWriter.SuggestAsync"/>. The token counts are reported even for a failure the
/// model answered, so the audit entry carries what the call cost.
/// </summary>
/// <param name="Outcome"></param>
/// <param name="Value">Suggested value (when written).</param>
/// <param name="Reason">One or two sentences on why (when written).</param>
/// <param name="Model">Model that answered (or was asked).</param>
/// <param name="InputTokens"></param>
/// <param name="OutputTokens"></param>
public sealed record ListingSuggestionResult(ReplyDraftOutcome Outcome, string Value, string Reason, string Model, long InputTokens = 0, long OutputTokens = 0)
{
    /// <summary>
    /// A failure before the model answered.
    /// </summary>
    /// <param name="outcome"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    public static ListingSuggestionResult Failure(ReplyDraftOutcome outcome, string model) => new(outcome, null, null, model);
}

/// <summary>
/// What the model is given for an App Store category suggestion. Every text is data for the prompt, never instructions.
/// </summary>
/// <param name="ApiKey">The organization's Anthropic key.</param>
/// <param name="AppName">storeasy's name of the app.</param>
/// <param name="Language">Language of the reason (the listing's primary language).</param>
/// <param name="Categories">The store's categories (id and title) to choose from.</param>
/// <param name="CurrentPrimary">Current primary category id, or null.</param>
/// <param name="CurrentSecondary">Current secondary category id, or null.</param>
/// <param name="Texts">Listing texts of the primary language (name, subtitle, description, keywords).</param>
/// <param name="Profile">The app's catalog profile as short lines.</param>
/// <param name="Instruction">The member's own note, or null.</param>
/// <param name="Model">Model to use; null for the instance default.</param>
public sealed record CategorySuggestionRequest(string ApiKey,
                                               string AppName,
                                               string Language,
                                               IReadOnlyList<(string Id, string Title)> Categories,
                                               string CurrentPrimary,
                                               string CurrentSecondary,
                                               IReadOnlyDictionary<string, string> Texts,
                                               IReadOnlyList<string> Profile,
                                               string Instruction,
                                               string Model = null);

/// <summary>
/// Result of <see cref="IListingSuggestionWriter.SuggestCategoriesAsync"/>. The ids are the model's answer as given; the
/// caller checks them against the store's list.
/// </summary>
/// <param name="Outcome"></param>
/// <param name="Primary">Suggested primary category id (when written).</param>
/// <param name="Secondary">Suggested secondary category id, or null for none.</param>
/// <param name="Reason">One or two sentences on why (when written).</param>
/// <param name="Model">Model that answered (or was asked).</param>
/// <param name="InputTokens"></param>
/// <param name="OutputTokens"></param>
public sealed record CategorySuggestionResult(ReplyDraftOutcome Outcome, string Primary, string Secondary, string Reason, string Model, long InputTokens = 0, long OutputTokens = 0);
