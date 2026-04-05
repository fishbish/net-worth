using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace net_worth_app.Data.Models;

[Index(nameof(AccountId), nameof(SnapshotDate), IsUnique = true)]
public class AccountSnapshot
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public DateOnly SnapshotDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AccountBalance { get; set; }

    public DateTime CreatedUtc { get; set; }

    public ICollection<InstrumentBalanceSnapshot> InstrumentBalances { get; set; } = new List<InstrumentBalanceSnapshot>();
}
