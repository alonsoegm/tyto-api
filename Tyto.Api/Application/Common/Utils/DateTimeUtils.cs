namespace Tyto.Api.Application.Common.Utils;

public static class DateTimeUtils
{
    public static DateTime UtcNow() => DateTime.UtcNow;

    public static DateTime? ParseUtc(string? value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var result)
            ? result
            : null;
}
