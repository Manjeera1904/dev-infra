namespace EI.API.Service.Data.Helpers.Util;

[System.Diagnostics.DebuggerDisplay("{DebuggerDisplayText,nq}")]
public readonly struct DateRange
{
    public static DateOnly EndOfTime { get; } = new(9999, 12, 31);
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public DateRange(DateOnly startDate, DateOnly endDate)
        : this(DT(startDate), DT(endDate)) { }

    public DateRange(DateTime startDate, DateTime endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    public bool IsNil => EndDate < StartDate;

    public DateTime StartDate { get; }
    public DateTime EndDate { get; }

    public DateOnly StartDateOnly => DO(StartDate);
    public DateOnly EndDateOnly => DO(EndDate);

    public bool Includes(DateTime date) => StartDate <= date && date <= EndDate;

    public bool Overlaps(DateRange that) => Overlaps(this, that);
    public bool Overlaps(DateTime start, DateTime end) => Overlaps(this, new DateRange(start, end));

    public DateRange Intersect(DateRange that) => Intersect(this, that);

    public static bool Overlaps(DateTime oneStart, DateTime oneEnd, DateTime twoStart, DateTime twoEnd)
        => Overlaps(new DateRange(oneStart, oneEnd), new DateRange(twoStart, twoEnd));

    public static bool Overlaps(DateRange one, DateRange two)
        => !one.IsNil && !two.IsNil &&
           (one.Includes(two.StartDate) || two.Includes(one.StartDate));

    public override string ToString() =>
        $"{StartDate:s} - {EndDate:s}";

    private string DebuggerDisplayText => ToString();

    public static DateRange Intersect(DateRange one, DateRange two)
    {
        if (one.IsNil || two.IsNil)
        {
            return new DateRange();
        }

        var start = one.StartDate > two.StartDate ? one.StartDate : two.StartDate;
        var end = one.EndDate < two.EndDate ? one.EndDate : two.EndDate;
        return new DateRange(start, end);
    }

    public static DateTime Min(params DateTime[] values)
        => values.Aggregate(DateTime.MaxValue, (agg, val) => agg < val ? agg : val);
    public static DateOnly Min(params DateOnly[] values)
        => values.Aggregate(DateOnly.MaxValue, (agg, val) => agg < val ? agg : val);

    public static DateTime Max(params DateTime[] values)
        => values.Aggregate(DateTime.MinValue, (agg, val) => agg > val ? agg : val);
    public static DateOnly Max(params DateOnly[] values)
        => values.Aggregate(DateOnly.MinValue, (agg, val) => agg > val ? agg : val);

    private static DateTime DT(DateOnly date) => new(date.Year, date.Month, date.Day);
    private static DateOnly DO(DateTime date) => new(date.Year, date.Month, date.Day);
}
