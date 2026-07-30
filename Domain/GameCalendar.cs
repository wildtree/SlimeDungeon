namespace SlimeDungeon.Domain;

/// <summary>One date in the in-world calendar.</summary>
public readonly record struct WorldDate(int Year, int MonthName_Index, int Day, string MonthName, bool IsLeapMonth);

/// <summary>
/// The world's own calendar: 30-day months, 12 months to a year. Because that only accounts for 360 days, the
/// reckoning slips about five days a year against the seasons, so every sixth year an intercalary 閏月 of 30
/// days is inserted between 日の上月 and 木の下月 — five days of drift over six years is the thirty days that
/// month puts back. Year 1, 木の上月 1日 is the Unix epoch, so a new adventurer starts on whatever today
/// converts to, and the day count advances one per return from a dungeon.
/// </summary>
public static class GameCalendar
{
    public const int DaysPerMonth = 30;
    public const int OrdinaryMonthsPerYear = 12;

    /// <summary>A leap month lands in every sixth year.</summary>
    public const int LeapYearInterval = 6;

    /// <summary>Zero-based position the leap month occupies: straight after 日の上月, the sixth month.</summary>
    public const int LeapMonthPosition = 6;

    public const string LeapMonthName = "閏月";

    private static readonly string[] OrdinaryMonthNames =
    [
        "木の上月", "火の上月", "土の上月", "金の上月", "水の上月", "日の上月",
        "木の下月", "火の下月", "土の下月", "金の下月", "水の下月", "日の下月",
    ];

    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static bool IsLeapYear(int year) => year % LeapYearInterval == 0;

    public static int MonthsInYear(int year) => IsLeapYear(year) ? OrdinaryMonthsPerYear + 1 : OrdinaryMonthsPerYear;

    public static int DaysInYear(int year) => MonthsInYear(year) * DaysPerMonth;

    /// <summary>Name of the month at <paramref name="position"/> (zero-based) within the given year.</summary>
    public static string MonthNameAt(int year, int position)
    {
        if (!IsLeapYear(year))
            return OrdinaryMonthNames[position];
        if (position < LeapMonthPosition)
            return OrdinaryMonthNames[position];
        if (position == LeapMonthPosition)
            return LeapMonthName;
        // Everything after the intercalary month keeps its ordinary name, shifted along by one slot.
        return OrdinaryMonthNames[position - 1];
    }

    /// <summary>Converts an absolute day number (1 = 元年 木の上月 1日) into a calendar date.</summary>
    public static WorldDate FromDayNumber(int dayNumber)
    {
        var remaining = Math.Max(0, dayNumber - 1);
        var year = 1;
        while (remaining >= DaysInYear(year))
        {
            remaining -= DaysInYear(year);
            year++;
        }

        var position = remaining / DaysPerMonth;
        var day = remaining % DaysPerMonth + 1;
        var isLeap = IsLeapYear(year) && position == LeapMonthPosition;
        return new WorldDate(year, position, day, MonthNameAt(year, position), isLeap);
    }

    /// <summary>Year 1 is written 元年, as is conventional for the first year of an era.</summary>
    public static string YearLabel(int year) => year == 1 ? "元年" : $"{year}年";

    /// <summary>e.g. "新暦57年 火の下月12日".</summary>
    public static string Format(int dayNumber)
    {
        var d = FromDayNumber(dayNumber);
        return $"新暦{YearLabel(d.Year)} {d.MonthName}{d.Day}日";
    }

    /// <summary>Without the era prefix, for tighter spots like a quest deadline.</summary>
    public static string FormatShort(int dayNumber)
    {
        var d = FromDayNumber(dayNumber);
        return $"{YearLabel(d.Year)} {d.MonthName}{d.Day}日";
    }

    /// <summary>Absolute day number for a real-world date, so a new character starts on today's date.</summary>
    public static int DayNumberFor(DateTime utcDate) =>
        (int)(utcDate.Date - Epoch.Date).TotalDays + 1;

    public static int Today() => DayNumberFor(DateTime.UtcNow);
}
