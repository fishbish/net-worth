using Microsoft.EntityFrameworkCore;
using NetWorth.Data;
using NetWorth.Data.Models;

namespace NetWorth.Services;

public class InstitutionService(NetWorthDbContext dbContext)
{
    public async Task<IReadOnlyList<Institution>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Institutions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAllAsync(IList<Institution> institutions, CancellationToken cancellationToken = default)
    {
        foreach (var institution in institutions)
            await SaveAsync(institution, cancellationToken);
    }

    public async Task<Institution> SaveAsync(Institution model, CancellationToken cancellationToken = default)
    {
        var name = model.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Institution name is required.");

        var entity = model.InstitutionId == Guid.Empty
            ? null
            : await dbContext.Institutions
                .SingleOrDefaultAsync(x => x.InstitutionId == model.InstitutionId, cancellationToken);

        if (entity is null)
        {
            entity = new Institution();
            dbContext.Institutions.Add(entity);
        }

        entity.Name = name;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(Guid institutionId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Institutions
            .SingleOrDefaultAsync(x => x.InstitutionId == institutionId, cancellationToken)
            ?? throw new InvalidOperationException("Institution not found.");

        dbContext.Institutions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
