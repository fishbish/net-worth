using Microsoft.EntityFrameworkCore;
using NetWorth.Data.Models;

namespace NetWorth.Data;

public class NetWorthDbContext(DbContextOptions<NetWorthDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountSnapshot> AccountSnapshots => Set<AccountSnapshot>();

    public DbSet<Institution> Institutions => Set<Institution>();

    public DbSet<Instrument> Instruments => Set<Instrument>();

    public DbSet<InstrumentSnapshot> InstrumentSnapshots => Set<InstrumentSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}

