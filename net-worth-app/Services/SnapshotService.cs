using Microsoft.EntityFrameworkCore;
using NetWorth.Data;
using NetWorth.Data.Models;

namespace NetWorth.Services;

public class SnapshotService(NetWorthDbContext dbContext, CurrentUserAccessor currentUserAccessor)
{
    public async Task<SnapshotEditorModel> GetEditorAsync(DateOnly snapshotDate, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new SnapshotAccountEditor
            {
                AccountId = x.AccountId,
                AccountName = x.Name,
                Category = x.Category,
                Type = x.Type,
                Instruments = x.Instruments
                    .OrderBy(i => i.Name)
                    .Select(i => new SnapshotInstrumentEditor
                    {
                        InstrumentId = i.InstrumentId,
                        InstrumentName = i.Name,
                        InstrumentType = i.Type
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(x => x.AccountId).ToArray();

        var snapshots = await dbContext.AccountSnapshots
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) && x.SnapshotDate == snapshotDate)
            .Select(x => new
            {
                x.AccountId,
                x.AccountBalance,
                InstrumentBalances = x.InstrumentSnapshots
                    .Select(i => new { i.InstrumentId, i.Balance })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var snapshotByAccountId = snapshots.ToDictionary(x => x.AccountId);

        foreach (var account in accounts)
        {
            if (!snapshotByAccountId.TryGetValue(account.AccountId, out var accountSnapshot))
            {
                continue;
            }

            account.AccountBalance = accountSnapshot.AccountBalance;

            var instrumentBalanceById = accountSnapshot.InstrumentBalances.ToDictionary(x => x.InstrumentId, x => x.Balance);
            foreach (var instrument in account.Instruments)
            {
                if (instrumentBalanceById.TryGetValue(instrument.InstrumentId, out var balance))
                {
                    instrument.Balance = balance;
                }
            }
        }

        return new SnapshotEditorModel
        {
            SnapshotDate = snapshotDate,
            Accounts = accounts
        };
    }

    public async Task SaveAsync(SnapshotEditorModel editor, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        if (editor.SnapshotDate == default)
        {
            throw new InvalidOperationException("Snapshot date is required.");
        }

        var requestedAccounts = editor.Accounts ?? [];
        if (requestedAccounts.Count == 0)
        {
            return;
        }

        var duplicateAccount = requestedAccounts
            .GroupBy(x => x.AccountId)
            .FirstOrDefault(x => x.Key == Guid.Empty || x.Count() > 1);
        if (duplicateAccount is not null)
        {
            throw new InvalidOperationException("Snapshot request contains duplicate or invalid accounts.");
        }

        var accountIds = requestedAccounts.Select(x => x.AccountId).ToArray();

        var ownedAccounts = await dbContext.Accounts
            .Where(x => x.UserId == userId && accountIds.Contains(x.AccountId))
            .Select(x => new
            {
                x.AccountId,
                InstrumentIds = x.Instruments.Select(i => i.InstrumentId).ToHashSet()
            })
            .ToListAsync(cancellationToken);

        if (ownedAccounts.Count != accountIds.Length)
        {
            throw new InvalidOperationException("One or more accounts were not found.");
        }

        var ownedAccountById = ownedAccounts.ToDictionary(x => x.AccountId);

        var existingHeaders = await dbContext.AccountSnapshots
            .Include(x => x.InstrumentSnapshots)
            .Where(x => accountIds.Contains(x.AccountId) && x.SnapshotDate == editor.SnapshotDate)
            .ToListAsync(cancellationToken);

        var headerByAccountId = existingHeaders.ToDictionary(x => x.AccountId);

        foreach (var requestedAccount in requestedAccounts)
        {
            var accountOwnership = ownedAccountById[requestedAccount.AccountId];
            var requestedInstruments = requestedAccount.Instruments ?? [];

            var duplicateInstrument = requestedInstruments
                .GroupBy(x => x.InstrumentId)
                .FirstOrDefault(x => x.Key == Guid.Empty || x.Count() > 1);
            if (duplicateInstrument is not null)
            {
                throw new InvalidOperationException($"Snapshot request contains duplicate or invalid instruments for account '{requestedAccount.AccountName}'.");
            }

            foreach (var instrument in requestedInstruments)
            {
                if (!accountOwnership.InstrumentIds.Contains(instrument.InstrumentId))
                {
                    throw new InvalidOperationException($"Instrument '{instrument.InstrumentName}' does not belong to account '{requestedAccount.AccountName}'.");
                }

                if (instrument.Balance is < 0m)
                {
                    throw new InvalidOperationException($"Instrument snapshot values must be positive for '{instrument.InstrumentName}'.");
                }
            }

            if (requestedAccount.AccountBalance is < 0m)
            {
                throw new InvalidOperationException($"Account snapshot values must be positive for '{requestedAccount.AccountName}'.");
            }

            var hasAccountBalance = requestedAccount.AccountBalance.HasValue;
            var requestedInstrumentValues = requestedInstruments.Where(x => x.Balance.HasValue).ToList();
            var hasInstrumentBalances = requestedInstrumentValues.Count > 0;

            if (hasAccountBalance && hasInstrumentBalances)
            {
                throw new InvalidOperationException($"Use either account-level or instrument-level values for '{requestedAccount.AccountName}' on the same date.");
            }

            headerByAccountId.TryGetValue(requestedAccount.AccountId, out var existingHeader);

            if (hasAccountBalance)
            {
                if (existingHeader is not null && existingHeader.InstrumentSnapshots.Count > 0)
                {
                    throw new InvalidOperationException($"Cannot save account-level value for '{requestedAccount.AccountName}' because instrument snapshots already exist for that date.");
                }

                var header = existingHeader ?? CreateAccountSnapshot(requestedAccount.AccountId, editor.SnapshotDate, headerByAccountId);
                header.AccountBalance = requestedAccount.AccountBalance.GetValueOrDefault();
                continue;
            }

            if (hasInstrumentBalances)
            {
                if (existingHeader is not null && existingHeader.AccountBalance.HasValue)
                {
                    throw new InvalidOperationException($"Cannot save instrument-level values for '{requestedAccount.AccountName}' because an account-level snapshot already exists for that date.");
                }

                var header = existingHeader ?? CreateAccountSnapshot(requestedAccount.AccountId, editor.SnapshotDate, headerByAccountId);
                header.AccountBalance = null;

                var existingInstrumentById = header.InstrumentSnapshots.ToDictionary(x => x.InstrumentId);
                foreach (var instrument in requestedInstruments)
                {
                    if (instrument.Balance.HasValue)
                    {
                        if (!existingInstrumentById.TryGetValue(instrument.InstrumentId, out var instrumentSnapshot))
                        {
                            instrumentSnapshot = new Data.Models.InstrumentSnapshot
                            {
                                AccountSnapshotId = header.AccountSnapshotId,
                                InstrumentId = instrument.InstrumentId,
                                CreatedUtc = DateTime.UtcNow
                            };
                            header.InstrumentSnapshots.Add(instrumentSnapshot);
                        }

                        instrumentSnapshot.Balance = instrument.Balance.Value;
                    }
                    else if (existingInstrumentById.TryGetValue(instrument.InstrumentId, out var existingInstrument))
                    {
                        dbContext.InstrumentSnapshots.Remove(existingInstrument);
                    }
                }

                continue;
            }

            if (existingHeader is not null)
            {
                dbContext.InstrumentSnapshots.RemoveRange(existingHeader.InstrumentSnapshots);
                dbContext.AccountSnapshots.Remove(existingHeader);
                headerByAccountId.Remove(requestedAccount.AccountId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Data.Models.AccountSnapshot CreateAccountSnapshot(
        Guid accountId,
        DateOnly snapshotDate,
        Dictionary<Guid, Data.Models.AccountSnapshot> headerByAccountId)
    {
        var snapshot = new Data.Models.AccountSnapshot
        {
            AccountId = accountId,
            SnapshotDate = snapshotDate,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.AccountSnapshots.Add(snapshot);
        headerByAccountId[accountId] = snapshot;
        return snapshot;
    }
}

public class SnapshotEditorModel
{
    public DateOnly SnapshotDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public List<SnapshotAccountEditor> Accounts { get; set; } = [];
}

public class SnapshotAccountEditor
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public AccountCategory Category { get; set; }
    public AccountType Type { get; set; }
    public decimal? AccountBalance { get; set; }
    public List<SnapshotInstrumentEditor> Instruments { get; set; } = [];
}

public class SnapshotInstrumentEditor
{
    public Guid InstrumentId { get; set; }
    public string InstrumentName { get; set; } = string.Empty;
    public InstrumentType InstrumentType { get; set; }
    public decimal? Balance { get; set; }
}
