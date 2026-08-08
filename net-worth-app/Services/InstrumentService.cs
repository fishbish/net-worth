using Microsoft.EntityFrameworkCore;
using NetWorth.Data;
using NetWorth.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace NetWorth.Services;

public class InstrumentService(NetWorthDbContext dbContext, CurrentUserAccessor currentUserAccessor)
{
    public async Task<IReadOnlyList<Instrument>> GetCatalogInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Instruments
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IList<Instrument> instruments, CancellationToken cancellationToken = default)
    {
        foreach (var instrument in instruments)
        {
            await SaveAsync(instrument, cancellationToken);
        }
    }

    public async Task<Instrument> SaveAsync(Instrument model, CancellationToken cancellationToken = default)
    {
        var entity = model.InstrumentId == Guid.Empty
            ? null
            : await dbContext.Instruments.SingleOrDefaultAsync(x => x.InstrumentId == model.InstrumentId, cancellationToken);

        if (entity is null)
        {
            entity = new Instrument
            {
                CreatedUtc = DateTime.UtcNow
            };
            dbContext.Instruments.Add(entity);
        }

        await ApplyInstrumentValuesAsync(entity, model.Name, model.Ticker, model.Type, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Instruments
            .Include(x => x.AccountInstruments)
            .SingleOrDefaultAsync(x => x.InstrumentId == instrumentId, cancellationToken)
            ?? throw new InvalidOperationException("Instrument not found.");

        if (entity.AccountInstruments.Count > 0)
        {
            throw new InvalidOperationException($"Cannot delete instrument '{entity.Name}' while it is linked to accounts.");
        }

        dbContext.Instruments.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InstrumentLookup>> GetByAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        await EnsureAccountOwnershipAsync(accountId, userId, cancellationToken);

        return await dbContext.AccountInstruments
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.Instrument.Name)
            .Select(x => new InstrumentLookup
            {
                AccountInstrumentId = x.AccountInstrumentId,
                InstrumentId = x.InstrumentId,
                Name = x.Instrument.Name,
                Ticker = x.Instrument.Ticker,
                Type = x.Instrument.Type,
                CreatedUtc = x.Instrument.CreatedUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InstrumentLookup>> GetAvailableForAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        await EnsureAccountOwnershipAsync(accountId, userId, cancellationToken);

        return await dbContext.Instruments
            .AsNoTracking()
            .Where(x => !x.AccountInstruments.Any(y => y.AccountId == accountId))
            .OrderBy(x => x.Name)
            .Select(x => new InstrumentLookup
            {
                InstrumentId = x.InstrumentId,
                Name = x.Name,
                Ticker = x.Ticker,
                Type = x.Type,
                CreatedUtc = x.CreatedUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task LinkAsync(Guid accountId, Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        await EnsureAccountOwnershipAsync(accountId, userId, cancellationToken);

        if (instrumentId == Guid.Empty)
        {
            throw new InvalidOperationException("Select an instrument to link.");
        }

        var instrument = await dbContext.Instruments
            .SingleOrDefaultAsync(x => x.InstrumentId == instrumentId, cancellationToken)
            ?? throw new InvalidOperationException("Selected instrument was not found.");

        var isAlreadyLinked = await dbContext.AccountInstruments
            .AnyAsync(x => x.AccountId == accountId && x.InstrumentId == instrumentId, cancellationToken);

        if (isAlreadyLinked)
        {
            throw new InvalidOperationException($"Instrument '{instrument.Name}' is already linked to this account.");
        }

        dbContext.AccountInstruments.Add(new AccountInstrument
        {
            AccountId = accountId,
            InstrumentId = instrumentId,
            CreatedUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InstrumentLookup> SaveAsync(Guid accountId, InstrumentUpsert instrument, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        await EnsureAccountOwnershipAsync(accountId, userId, cancellationToken);

        if (instrument.InstrumentId == Guid.Empty)
        {
            var entity = new Instrument
            {
                CreatedUtc = DateTime.UtcNow
            };

            await ApplyInstrumentValuesAsync(entity, instrument.Name, instrument.Ticker, instrument.Type, cancellationToken);

            var link = new AccountInstrument
            {
                AccountId = accountId,
                Instrument = entity,
                CreatedUtc = DateTime.UtcNow
            };

            dbContext.AccountInstruments.Add(link);
            await dbContext.SaveChangesAsync(cancellationToken);

            return CreateLookup(link.AccountInstrumentId, entity);
        }

        var accountInstrument = await dbContext.AccountInstruments
            .Include(x => x.Instrument)
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.InstrumentId == instrument.InstrumentId, cancellationToken)
            ?? throw new InvalidOperationException("Instrument not found for this account.");

        await ApplyInstrumentValuesAsync(accountInstrument.Instrument, instrument.Name, instrument.Ticker, instrument.Type, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateLookup(accountInstrument.AccountInstrumentId, accountInstrument.Instrument);
    }

    public async Task UnlinkAsync(Guid accountId, Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        await EnsureAccountOwnershipAsync(accountId, userId, cancellationToken);

        var accountInstrument = await dbContext.AccountInstruments
            .Include(x => x.Instrument)
            .Include(x => x.Snapshots)
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.InstrumentId == instrumentId, cancellationToken)
            ?? throw new InvalidOperationException("Instrument not found for this account.");

        if (accountInstrument.Snapshots.Count > 0)
        {
            throw new InvalidOperationException($"Cannot unlink instrument '{accountInstrument.Instrument.Name}' from this account because snapshot history exists.");
        }

        dbContext.AccountInstruments.Remove(accountInstrument);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyInstrumentValuesAsync(
        Instrument entity,
        string? rawName,
        string? rawTicker,
        InstrumentType type,
        CancellationToken cancellationToken)
    {
        var name = rawName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Instrument name is required.");
        }

        var ticker = string.IsNullOrWhiteSpace(rawTicker)
            ? null
            : rawTicker.Trim();

        var duplicate = await dbContext.Instruments
            .FirstOrDefaultAsync(x => (x.Name == name || x.Ticker == ticker) && x.InstrumentId != entity.InstrumentId, cancellationToken);

        if (duplicate != null)
        {
            if(duplicate.Name == name) throw new InvalidOperationException("Instrument name must be unique.");
            if(duplicate.Ticker == ticker) throw new InvalidOperationException("Instrument ticker must be unique.");
        }

        entity.Name = name;
        entity.Ticker = ticker;
        entity.Type = type;
    }

    private async Task EnsureAccountOwnershipAsync(Guid accountId, string userId, CancellationToken cancellationToken)
    {
        var accountExists = await dbContext.Accounts
            .AnyAsync(x => x.AccountId == accountId && x.UserId == userId, cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException("Account not found.");
        }
    }

    private static InstrumentLookup CreateLookup(Guid accountInstrumentId, Instrument entity)
    {
        return new InstrumentLookup
        {
            AccountInstrumentId = accountInstrumentId,
            InstrumentId = entity.InstrumentId,
            Name = entity.Name,
            Ticker = entity.Ticker,
            Type = entity.Type,
            CreatedUtc = entity.CreatedUtc
        };
    }
}

public class InstrumentLookup
{
    public Guid AccountInstrumentId { get; set; }
    public Guid InstrumentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Ticker { get; set; }
    public InstrumentType Type { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(Ticker) ? $"{Name} ({Type})" : $"{Name} ({Ticker})";
}

public class InstrumentUpsert
{
    public Guid InstrumentId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Ticker { get; set; }

    public InstrumentType Type { get; set; } = InstrumentType.Stock;
}
