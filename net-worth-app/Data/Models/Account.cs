using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NetWorth.Data.Models;

[Index(nameof(UserId), nameof(Name), IsUnique = true)]
public class Account
{
    public Guid AccountId { get; set; }

    [Required]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AccountCategory Category { get; set; }

    public AccountType Type { get; set; }

    public Guid InstitutionId { get; set; }

    public Institution Institution { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public ICollection<AccountSnapshot> Snapshots { get; set; } = new List<AccountSnapshot>();

    public ICollection<Instrument> Instruments { get; set; } = new List<Instrument>();
}

