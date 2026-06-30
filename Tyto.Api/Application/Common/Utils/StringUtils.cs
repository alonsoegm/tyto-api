namespace Tyto.Api.Application.Common.Utils;

public static class StringUtils
{
    public static string Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value ?? string.Empty
            : value[..maxLength];

    public static string ToSlug(string value) =>
        System.Text.RegularExpressions.Regex
            .Replace(value.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-")
            .Trim('-');
}
