namespace EI.API.Service.Rest.Helpers.Model;

public class EntityHistoryDto<TEntity>
{
    public TEntity Entity { get; set; } = default!;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public class EntityWithTranslationHistoryDto<TEntity, TTranslation>
{
    public EntityHistoryDto<TEntity> Entity { get; set; } = default!;
    public List<EntityHistoryDto<TTranslation>> Translations { get; set; } = default!;
}
