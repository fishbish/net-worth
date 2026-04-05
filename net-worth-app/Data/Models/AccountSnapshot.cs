using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace NetWorth.Data.Models;

[Index(nameof(AccountId), nameof(SnapshotDate), IsUnique = true)]
public class AccountSnapshot
{
    public Guid AccountSnapshotId { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public DateOnly SnapshotDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AccountBalance { get; set; }

    public DateTime CreatedUtc { get; set; }

    public ICollection<InstrumentSnapshot> InstrumentSnapshots { get; set; } = new List<InstrumentSnapshot>();
}

