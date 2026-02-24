namespace EI.API.Service.Data.Helpers.Model;

public abstract class BaseDatabaseEntityWithTranslation<TTranslation> : BaseDatabaseEntity, IDatabaseEntityWithTranslation<TTranslation>
    where TTranslation : IDatabaseTranslationsEntity
{
    public List<TTranslation> Translations { get; set; } = [];
}