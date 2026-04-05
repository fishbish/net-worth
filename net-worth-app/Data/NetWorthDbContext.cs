using Microsoft.EntityFrameworkCore;
using net_worth_app.Data.Models;

namespace net_worth_app.Data;

public class NetWorthDbContext(DbContextOptions<NetWorthDbContext> options) : DbContext(options)
{
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountSnapshot> AccountSnapshots => Set<AccountSnapshot>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<InstrumentBalanceSnapshot> InstrumentBalanceSnapshots => Set<InstrumentBalanceSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
