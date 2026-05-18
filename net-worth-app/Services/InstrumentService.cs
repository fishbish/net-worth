using Microsoft.EntityFrameworkCore;
using NetWorth.Data;
using NetWorth.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace NetWorth.Services;

public class InstrumentService(NetWorthDbContext dbContext, CurrentUserAccessor currentUserAccessor)
{
    public async Task<IReadOnlyList<InstrumentLookup>> GetByAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        await EnsureAccountOwnershipAsync(accountId, userId, cancellationToken);

        return await dbContext.Instruments
            .AsNoTracking()
            .Where(x => x.AccountId == accountId)
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

    public async Task<InstrumentLookup> SaveAsync(Guid accountId, InstrumentUpsert instrument, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        await EnsureAccountOwnershipAsync(accountId, userId, cancellationToken);

        var name = instrument.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Instrument name is required.");
        }

        var entity = instrument.InstrumentId == Guid.Empty
            ? null
            : await dbContext.Instruments
                .SingleOrDefaultAsync(x => x.InstrumentId == instrument.InstrumentId && x.AccountId == accountId, cancellationToken);

        if (entity is null && instrument.InstrumentId != Guid.Empty)
        {
            throw new InvalidOperationException("Instrument not found for this account.");
        }

        if (entity is null)
        {
            entity = new Instrument
            {
                AccountId = accountId,
                CreatedUtc = DateTime.UtcNow
            };
            dbContext.Instruments.Add(entity);
        }

        entity.Name = name;
        entity.Ticker = string.IsNullOrWhiteSpace(instrument.Ticker) ? null : instrument.Ticker.Trim();
        entity.Type = instrument.Type;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InstrumentLookup
        {
            InstrumentId = entity.InstrumentId,
            Name = entity.Name,
            Ticker = entity.Ticker,
            Type = entity.Type,
            CreatedUtc = entity.CreatedUtc
        };
    }

    public async Task DeleteAsync(Guid accountId, Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();

        var entity = await dbContext.Instruments
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.InstrumentId == instrumentId && x.AccountId == accountId, cancellationToken)
            ?? throw new InvalidOperationException("Instrument not found.");

        if (entity.Account.UserId != userId)
        {
            throw new InvalidOperationException("Instrument not found.");
        }

        dbContext.Instruments.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
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
}

public class InstrumentLookup
{
    public Guid InstrumentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Ticker { get; set; }
    public InstrumentType Type { get; set; }
    public DateTime CreatedUtc { get; set; }
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
