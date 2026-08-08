using Microsoft.EntityFrameworkCore;
using NetWorth.Data.Models;
using System.Linq;

namespace NetWorth.Data;

public class NetWorthDbContext(DbContextOptions<NetWorthDbContext> options) : DbContext(options)
{
    public DbSet<AccountInstrument> AccountInstruments => Set<AccountInstrument>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<AccountSnapshot> AccountSnapshots => Set<AccountSnapshot>();

    public DbSet<Institution> Institutions => Set<Institution>();

    public DbSet<Instrument> Instruments => Set<Instrument>();

    public DbSet<InstrumentSnapshot> InstrumentSnapshots => Set<InstrumentSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }

        modelBuilder.Entity<AccountInstrument>(b =>
        {
            b.HasOne(x => x.Account)
                .WithMany(x => x.AccountInstruments)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);            
        });

        modelBuilder.Entity<Instrument>(b =>
        {
            b.HasIndex(x => x.Name)
                .IsUnique();
            b.HasIndex(x => x.Ticker)
                .IsUnique()
                .HasFilter("[Ticker] IS NOT NULL");
        });
    }
}
