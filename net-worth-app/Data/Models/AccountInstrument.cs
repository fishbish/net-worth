using Microsoft.EntityFrameworkCore;

namespace NetWorth.Data.Models;

[Index(nameof(AccountId), nameof(InstrumentId), IsUnique = true)]
public class AccountInstrument
{
    public Guid AccountInstrumentId { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid InstrumentId { get; set; }

    public Instrument Instrument { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public ICollection<InstrumentSnapshot> Snapshots { get; set; } = new List<InstrumentSnapshot>();
}
