using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Model;

[PrimaryKey(nameof(Id), nameof(CultureCode))]
public abstract class BaseDatabaseTranslationsEntity<TEntity> : BaseDatabaseEntity, IDatabaseTranslationsEntity
    where TEntity : IDatabaseEntity
{
    [Required]
    [MinLength(2)]
    [MaxLength(20)]
    public string CultureCode { get; set; } = null!;

    [ExcludeFromCodeCoverage]
    [ForeignKey(nameof(Id))]
    public TEntity Entity { get; set; } = default!;
}