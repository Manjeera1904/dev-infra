using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace EI.API.Service.Data.Helpers.Model;

[PrimaryKey(nameof(Id))]
public abstract class BaseDatabaseEntity : IDatabaseEntity
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string UpdatedBy { get; set; } = null!;

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}