using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NetWorth.Data.Models;

[Index(nameof(AccountId), nameof(Name), IsUnique = true)]
public class Instrument
{
    public Guid InstrumentId { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Ticker { get; set; }

    public InstrumentType Type { get; set; }

    public DateTime CreatedUtc { get; set; }

    public ICollection<InstrumentSnapshot> Snapshots { get; set; } = new List<InstrumentSnapshot>();
}

