namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Line diff of two normalized texts for the rule watcher: the lines only in the old text and the lines only in the new
/// one (a multiset comparison, which is what a reviewer needs to spot a changed date or requirement).
/// </summary>
public static class TextDiff
{
    /// <summary>
    /// Lines removed and added between <paramref name="before"/> and <paramref name="after"/>, at most
    /// <paramref name="maxLines"/> each.
    /// </summary>
    /// <param name="before"></param>
    /// <param name="after"></param>
    /// <param name="maxLines"></param>
    /// <returns></returns>
    public static (List<string> Removed, List<string> Added) Lines(string before, string after, int maxLines = 200)
    {
        var oldLines = Split(before);
        var newLines = Split(after);

        return (Except(oldLines, newLines).Take(maxLines).ToList(), Except(newLines, oldLines).Take(maxLines).ToList());
    }

    private static List<string> Split(string text) => [.. (text ?? string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)];

    private static IEnumerable<string> Except(List<string> source, List<string> other)
    {
        var remaining = other.GroupBy(l => l).ToDictionary(g => g.Key, g => g.Count());

        foreach (var line in source)
        {
            if (remaining.TryGetValue(line, out var count) && count > 0)
            {
                remaining[line] = count - 1;
                continue;
            }

            yield return line;
        }
    }
}
