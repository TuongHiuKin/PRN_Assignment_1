using AIVES.DAL.Context;
using AIVES.DAL.Entities;
using AIVES.DAL.Repositories.Interfaces;
using AIVES.DAL.Repositories.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DAL.Repositories.Implementations;

public sealed class TagRepository(AIVESDbContext context) : ITagRepository
{
    private async Task SaveChangesSafeAsync(CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public Task<List<Tag>> GetAllAsync(CancellationToken ct = default) =>
        context.Tags.AsNoTracking().OrderBy(x => x.TagName).ThenBy(x => x.TagId).ToListAsync(ct);

    public Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default) =>
        context.Tags.AsNoTracking().SingleOrDefaultAsync(x => x.TagId == id, ct);

    public Task<List<Tag>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var distinctIds = ids.Distinct().ToArray();
        return context.Tags.AsNoTracking().Where(x => distinctIds.Contains(x.TagId))
            .OrderBy(x => x.TagName).ThenBy(x => x.TagId).ToListAsync(ct);
    }

    public Task<bool> IsUsedAsync(int id, CancellationToken ct = default) =>
        context.NewsTags.AnyAsync(x => x.TagId == id, ct);

    public async Task<DeleteResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await context.Tags.SingleOrDefaultAsync(x => x.TagId == id, ct);
        if (entity is null) return DeleteResult.NotFound;
        if (await IsUsedAsync(id, ct)) return DeleteResult.InUse;

        context.Tags.Remove(entity);
        try
        {
            await SaveChangesSafeAsync(ct);
            return DeleteResult.Deleted;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            context.ChangeTracker.Clear();
            return DeleteResult.InUse;
        }
    }
}
