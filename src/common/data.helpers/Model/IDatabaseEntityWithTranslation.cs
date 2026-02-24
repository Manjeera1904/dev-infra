namespace EI.API.Service.Data.Helpers.Model;

public interface IDatabaseEntityWithTranslation<TTranslation> : IDatabaseEntity
    where TTranslation : IDatabaseTranslationsEntity
{
    List<TTranslation> Translations { get; set; }
}