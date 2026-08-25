namespace POS.Application.Closing;

public static class MuscatClock
{
    // Use the server/container's configured local time zone. This avoids
    // relying on OS-specific time-zone IDs or optional tzdata packages.
    public static TimeZoneInfo TimeZone => TimeZoneInfo.Local;

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);
    public static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);
}
