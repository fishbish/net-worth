using Microsoft.EntityFrameworkCore;
using NetWorth.Data;
using NetWorth.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace NetWorth.Services;

public class AccountService(NetWorthDbContext dbContext, CurrentUserAccessor currentUserAccessor)
{
    public async Task<IReadOnlyList<AccountLookup>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();

        return await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new AccountLookup
            {
                AccountId = x.AccountId,
                Name = x.Name,
                Category = x.Category,
                Type = x.Type,
                InstitutionId = x.InstitutionId,
                InstitutionName = x.Institution.Name,
                CreatedUtc = x.CreatedUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountLookup> SaveAsync(AccountUpsert account, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        var name = account.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Account name is required.");
        }

        if (account.InstitutionId == Guid.Empty)
        {
            throw new InvalidOperationException("Institution is required.");
        }

        var institutionExists = await dbContext.Institutions
            .AnyAsync(x => x.InstitutionId == account.InstitutionId, cancellationToken);

        if (!institutionExists)
        {
            throw new InvalidOperationException("Selected institution does not exist.");
        }

        var entity = await dbContext.Accounts
            .SingleOrDefaultAsync(x => x.AccountId == account.AccountId && x.UserId == userId, cancellationToken);

        if (entity is null)
        {
            entity = new Account
            {
                UserId = userId,
                CreatedUtc = DateTime.UtcNow
            };
            dbContext.Accounts.Add(entity);
        }

        entity.Name = name;
        entity.Category = account.Category;
        entity.Type = account.Type;
        entity.InstitutionId = account.InstitutionId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.AccountId == entity.AccountId)
            .Select(x => new AccountLookup
            {
                AccountId = x.AccountId,
                Name = x.Name,
                Category = x.Category,
                Type = x.Type,
                InstitutionId = x.InstitutionId,
                InstitutionName = x.Institution.Name,
                CreatedUtc = x.CreatedUtc
            })
            .SingleAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var userId = currentUserAccessor.GetRequiredUserId();
        var entity = await dbContext.Accounts
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Account not found.");

        dbContext.Accounts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public class AccountLookup
{
    public Guid AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountCategory Category { get; set; }
    public AccountType Type { get; set; }
    public Guid InstitutionId { get; set; }
    public string InstitutionName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

public class AccountUpsert
{
    public Guid AccountId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AccountCategory Category { get; set; } = AccountCategory.Asset;

    public AccountType Type { get; set; } = AccountType.Cash;

    [Required]
    public Guid InstitutionId { get; set; }
}
