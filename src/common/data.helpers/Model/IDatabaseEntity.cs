namespace EI.API.Service.Data.Helpers.Model;

public interface IDatabaseEntity
{
    public Guid Id { get; set; }
    public string UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; }
}