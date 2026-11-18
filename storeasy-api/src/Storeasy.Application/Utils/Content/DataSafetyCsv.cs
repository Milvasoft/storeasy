using Storeasy.Domain.Apps;
using System.Text;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// The Data safety CSV Play accepts through <c>applications.dataSafety</c> (the same columns Play Console exports),
/// generated from the app's saved <see cref="DataSafetyAnswers"/>.
/// </summary>
public static class DataSafetyCsv
{
    /// <summary>
    /// Header row of the CSV.
    /// </summary>
    public const string Header = "Question ID (machine readable),Response ID (machine readable),Response value,Answer requirement,Human-friendly question label";

    /// <summary>
    /// The CSV of <paramref name="answers"/> (CRLF line ends, fields quoted when needed).
    /// </summary>
    /// <param name="answers"></param>
    /// <returns></returns>
    public static string Generate(DataSafetyAnswers answers)
    {
        var builder = new StringBuilder(Header).Append("\r\n");

        foreach (var row in answers.Rows)
        {
            builder.Append(Field(row.QuestionId)).Append(',')
                   .Append(Field(row.ResponseId)).Append(',')
                   .Append(Field(row.ResponseValue)).Append(',')
                   .Append(',')
                   .Append(Field(row.Label)).Append("\r\n");
        }

        return builder.ToString();
    }

    private static string Field(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
