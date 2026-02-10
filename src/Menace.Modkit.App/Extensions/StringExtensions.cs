namespace Menace.Modkit.App.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Truncates a string to the specified maximum length, appending "..." if truncated.
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (maxLength <= 3) return value.Length <= maxLength ? value : "...";
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
