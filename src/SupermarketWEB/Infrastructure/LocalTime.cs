namespace SupermarketWEB.Infrastructure;

/// <summary>
/// Conversión entre las fechas UTC que se almacenan y los días locales que ve el usuario.
/// </summary>
public static class LocalTime
{
    public static DateOnly Today(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    /// <summary>
    /// Instante UTC en el que empieza el día local indicado.
    /// </summary>
    public static DateTime StartOfDayUtc(this TimeProvider timeProvider, DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), timeProvider.LocalTimeZone);

    public static DateTime ToLocal(this TimeProvider timeProvider, DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeProvider.LocalTimeZone);
}
