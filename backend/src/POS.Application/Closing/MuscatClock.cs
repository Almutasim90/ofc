namespace POS.Application.Closing;

public static class MuscatClock
{
    public static TimeZoneInfo TimeZone
    {
        get
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Muscat"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Arabian Standard Time"); }
        }
    }

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);
    public static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZone);
}
