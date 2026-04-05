using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NetWorth.Data.Models;

[Index(nameof(Name), IsUnique = true)]
public class Institution
{
    public Guid InstitutionId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}

