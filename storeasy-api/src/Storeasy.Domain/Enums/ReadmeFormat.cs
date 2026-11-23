using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Format a registry serves a package README in. storeasy shows it as text either way; HTML is reduced to its text.
/// </summary>
public enum ReadmeFormat : byte
{
    /// <summary>
    /// Markdown (npm, NuGet, PyPI with <c>text/markdown</c>).
    /// </summary>
    [Description("Markdown")]
    Markdown = 1,

    /// <summary>
    /// HTML rendered by the registry (crates.io); storeasy keeps its text only.
    /// </summary>
    [Description("HTML")]
    Html = 2,

    /// <summary>
    /// Plain text or reStructuredText (PyPI).
    /// </summary>
    [Description("Text")]
    Text = 3,
}
