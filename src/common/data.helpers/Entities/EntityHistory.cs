using EI.API.Service.Data.Helpers.Model;

namespace EI.API.Service.Data.Helpers.Entities;

public class EntityHistory<TEntity>
    where TEntity : IDatabaseEntity
{
    public TEntity Entity { get; set; } = default!;

    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}
