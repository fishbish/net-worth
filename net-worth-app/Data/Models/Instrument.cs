using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NetWorth.Data.Models;

public class Instrument
{
    public Guid InstrumentId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Ticker { get; set; }

    public InstrumentType Type { get; set; }

    public DateTime CreatedUtc { get; set; }

    public ICollection<AccountInstrument> AccountInstruments { get; set; } = new List<AccountInstrument>();
}
