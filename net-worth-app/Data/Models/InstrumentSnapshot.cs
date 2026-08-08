using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace NetWorth.Data.Models;

[Index(nameof(AccountSnapshotId), nameof(AccountInstrumentId), IsUnique = true)]
public class InstrumentSnapshot
{
    public Guid InstrumentSnapshotId { get; set; }

    public Guid AccountSnapshotId { get; set; }

    public AccountSnapshot AccountSnapshot { get; set; } = null!;

    public Guid AccountInstrumentId { get; set; }

    public AccountInstrument AccountInstrument { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    public DateTime CreatedUtc { get; set; }
}
