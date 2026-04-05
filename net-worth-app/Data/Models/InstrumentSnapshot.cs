using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace NetWorth.Data.Models;

[Index(nameof(AccountSnapshotId), nameof(InstrumentId), IsUnique = true)]
public class InstrumentSnapshot
{
    public Guid InstrumentSnapshotId { get; set; }

    public Guid AccountSnapshotId { get; set; }

    public AccountSnapshot AccountSnapshot { get; set; } = null!;

    public Guid InstrumentId { get; set; }

    public Instrument Instrument { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    public DateTime CreatedUtc { get; set; }
}

