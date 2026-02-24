namespace EI.API.Service.Data.Helpers.Model;

public interface IDatabaseTranslationsEntity : IDatabaseEntity
{
    public string CultureCode { get; set; }
}