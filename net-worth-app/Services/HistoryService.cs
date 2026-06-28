using Microsoft.EntityFrameworkCore;
using NetWorth.Data;
using NetWorth.Data.Models;

namespace NetWorth.Services;

public class HistoryService(NetWorthDbContext dbContext, CurrentUserAccessor currentUserAccessor)
{
    public async Task<HistoryPageData> GetHistoryAsync(HistoryRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new HistoryAccountOption
            {
                AccountId = x.AccountId,
                Name = x.Name,
                Category = x.Category,
                Type = x.Type
            })
            .ToListAsync(cancellationToken);

        var availableDateRange = await dbContext.AccountSnapshots
            .AsNoTracking()
            .Where(x => x.Account.UserId == userId)
            .GroupBy(x => 1)
            .Select(x => new
            {
                MinDate = x.Min(y => y.SnapshotDate),
                MaxDate = x.Max(y => y.SnapshotDate)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var defaultDate = DateOnly.FromDateTime(DateTime.Today);
        var minAvailableDate = availableDateRange?.MinDate ?? defaultDate;
        var maxAvailableDate = availableDateRange?.MaxDate ?? defaultDate;

        var startDate = request.StartDate ?? minAvailableDate;
        var endDate = request.EndDate ?? maxAvailableDate;

        if (startDate > endDate)
        {
            throw new InvalidOperationException("Start date must be on or before end date.");
        }

        var selectedAccount = request.AccountId.HasValue
            ? accounts.SingleOrDefault(x => x.AccountId == request.AccountId.Value)
            : null;

        if (request.AccountId.HasValue && selectedAccount is null)
        {
            throw new InvalidOperationException("Selected account was not found.");
        }

        var instruments = selectedAccount is null
            ? []
            : await dbContext.Instruments
                .AsNoTracking()
                .Where(x => x.AccountId == selectedAccount.AccountId)
                .OrderBy(x => x.Name)
                .Select(x => new HistoryInstrumentOption
                {
                    InstrumentId = x.InstrumentId,
                    Name = x.Name,
                    Ticker = x.Ticker,
                    Type = x.Type
                })
                .ToListAsync(cancellationToken);

        var selectedInstrument = selectedAccount is not null && request.InstrumentId.HasValue
            ? instruments.SingleOrDefault(x => x.InstrumentId == request.InstrumentId.Value)
            : null;

        if (selectedAccount is not null && request.InstrumentId.HasValue && selectedInstrument is null)
        {
            throw new InvalidOperationException("Selected instrument was not found for this account.");
        }

        var accountIds = selectedAccount is null
            ? accounts.Select(x => x.AccountId).ToArray()
            : [selectedAccount.AccountId];

        var snapshots = availableDateRange is null || accountIds.Length == 0
            ? []
            : await dbContext.AccountSnapshots
                .AsNoTracking()
                .Where(x =>
                    accountIds.Contains(x.AccountId) &&
                    x.SnapshotDate >= startDate &&
                    x.SnapshotDate <= endDate)
                .OrderBy(x => x.SnapshotDate)
                .ThenBy(x => x.Account.Name)
                .Select(x => new HistoryAccountSnapshot
                {
                    AccountId = x.AccountId,
                    SnapshotDate = x.SnapshotDate,
                    AccountCategory = x.Account.Category,
                    AccountBalance = x.AccountBalance,
                    InstrumentSnapshots = x.InstrumentSnapshots
                        .OrderBy(i => i.Instrument.Name)
                        .Select(i => new HistoryInstrumentSnapshot
                        {
                            InstrumentId = i.InstrumentId,
                            Balance = i.Balance
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken);

        return new HistoryPageData
        {
            HasSnapshotData = availableDateRange is not null,
            StartDate = startDate,
            EndDate = endDate,
            Accounts = accounts,
            SelectedAccountId = selectedAccount?.AccountId,
            SelectedAccountCategory = selectedAccount?.Category,
            Instruments = instruments,
            SelectedInstrumentId = selectedInstrument?.InstrumentId,
            Title = BuildTitle(selectedAccount, selectedInstrument),
            Subtitle = BuildSubtitle(selectedAccount, selectedInstrument),
            Points = BuildPoints(snapshots, selectedAccount, selectedInstrument)
        };
    }

    private static string BuildTitle(HistoryAccountOption? selectedAccount, HistoryInstrumentOption? selectedInstrument)
    {
        if (selectedInstrument is not null)
        {
            return $"{selectedInstrument.DisplayName} History";
        }

        if (selectedAccount is not null)
        {
            return $"{selectedAccount.Name} History";
        }

        return "Net Worth History";
    }

    private static string BuildSubtitle(HistoryAccountOption? selectedAccount, HistoryInstrumentOption? selectedInstrument)
    {
        if (selectedInstrument is not null && selectedAccount is not null)
        {
            return $"Instrument balances for {selectedAccount.Name}.";
        }

        if (selectedAccount is not null)
        {
            return "Account totals use instrument snapshots when they exist, otherwise the account-level balance for that date.";
        }

        return "Total net worth is calculated as assets minus liabilities for each snapshot date.";
    }

    private static List<HistoryPoint> BuildPoints(
        List<HistoryAccountSnapshot> snapshots,
        HistoryAccountOption? selectedAccount,
        HistoryInstrumentOption? selectedInstrument)
    {
        if (selectedInstrument is not null)
        {
            return snapshots
                .SelectMany(x => x.InstrumentSnapshots
                    .Where(y => y.InstrumentId == selectedInstrument.InstrumentId)
                    .Select(y => new HistoryPoint
                    {
                        Date = x.SnapshotDate,
                        Value = y.Balance
                    }))
                .OrderBy(x => x.Date)
                .ToList();
        }

        if (selectedAccount is not null)
        {
            return snapshots
                .Select(x => new
                {
                    x.SnapshotDate,
                    Value = ResolveAccountValue(x)
                })
                .Where(x => x.Value.HasValue)
                .Select(x => new HistoryPoint
                {
                    Date = x.SnapshotDate,
                    Value = x.Value!.Value
                })
                .OrderBy(x => x.Date)
                .ToList();
        }

        return snapshots
            .Select(x => new
            {
                x.SnapshotDate,
                SignedValue = ResolveSignedAccountValue(x)
            })
            .Where(x => x.SignedValue.HasValue)
            .GroupBy(x => x.SnapshotDate)
            .Select(x => new HistoryPoint
            {
                Date = x.Key,
                Value = x.Sum(y => y.SignedValue!.Value)
            })
            .OrderBy(x => x.Date)
            .ToList();
    }

    private static decimal? ResolveAccountValue(HistoryAccountSnapshot snapshot)
    {
        if (snapshot.InstrumentSnapshots.Count > 0)
        {
            return snapshot.InstrumentSnapshots.Sum(x => x.Balance);
        }

        return snapshot.AccountBalance;
    }

    private static decimal? ResolveSignedAccountValue(HistoryAccountSnapshot snapshot)
    {
        var value = ResolveAccountValue(snapshot);
        if (!value.HasValue)
        {
            return null;
        }

        return snapshot.AccountCategory == AccountCategory.Asset
            ? value.Value
            : -value.Value;
    }
}

public class HistoryRequest
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? InstrumentId { get; set; }
}

public class HistoryPageData
{
    public bool HasSnapshotData { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<HistoryAccountOption> Accounts { get; set; } = [];
    public Guid? SelectedAccountId { get; set; }
    public AccountCategory? SelectedAccountCategory { get; set; }
    public List<HistoryInstrumentOption> Instruments { get; set; } = [];
    public Guid? SelectedInstrumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public List<HistoryPoint> Points { get; set; } = [];
}

public class HistoryAccountOption
{
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountCategory Category { get; set; }
    public AccountType Type { get; set; }
    public string DisplayName => $"{Name} ({Category} · {Type})";
}

public class HistoryInstrumentOption
{
    public Guid InstrumentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Ticker { get; set; }
    public InstrumentType Type { get; set; }
    public string DisplayName => string.IsNullOrWhiteSpace(Ticker) ? $"{Name} ({Type})" : $"{Name} ({Ticker})";
}

public class HistoryPoint
{
    public DateOnly Date { get; set; }
    public decimal Value { get; set; }
}

internal class HistoryAccountSnapshot
{
    public Guid AccountId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public AccountCategory AccountCategory { get; set; }
    public decimal? AccountBalance { get; set; }
    public List<HistoryInstrumentSnapshot> InstrumentSnapshots { get; set; } = [];
}

internal class HistoryInstrumentSnapshot
{
    public Guid InstrumentId { get; set; }
    public decimal Balance { get; set; }
}
