namespace EI.API.Service.Data.Helpers.Model;

public interface IDateRange
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}