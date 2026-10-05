using AIVES.DAL.Context;
using AIVES.DAL.Entities;
using AIVES.DAL.Repositories.Interfaces;
using AIVES.DAL.Repositories.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DAL.Repositories.Implementations;

public sealed class CategoryRepository(AIVESDbContext context) : ICategoryRepository
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

    public async Task<List<Category>> SearchAsync(string? keyword = null, CancellationToken ct = default)
    {
        var query = context.Categories.AsNoTracking();
        var term = keyword?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(x => x.CategoryName.Contains(term) ||
                (x.CategoryDescription != null && x.CategoryDescription.Contains(term)));
        }

        return await query.OrderBy(x => x.CategoryName).ThenBy(x => x.CategoryId).ToListAsync(ct);
    }

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct = default) =>
        context.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.CategoryId == id, ct);

    public Task<bool> HasNewsAsync(int id, CancellationToken ct = default) =>
        context.NewsArticles.AnyAsync(x => x.CategoryId == id, ct);

    public async Task<int> CreateAsync(string name, string? description, CancellationToken ct = default)
    {
        var entity = new Category { CategoryName = name, CategoryDescription = description };
        context.Categories.Add(entity);
        await SaveChangesSafeAsync(ct);
        return entity.CategoryId;
    }

    public async Task<bool> UpdateAsync(int id, string name, string? description, CancellationToken ct = default)
    {
        var entity = await context.Categories.SingleOrDefaultAsync(x => x.CategoryId == id, ct);
        if (entity is null) return false;
        entity.CategoryName = name;
        entity.CategoryDescription = description;
        await SaveChangesSafeAsync(ct);
        return true;
    }

    public async Task<DeleteResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await context.Categories.SingleOrDefaultAsync(x => x.CategoryId == id, ct);
        if (entity is null) return DeleteResult.NotFound;
        if (await HasNewsAsync(id, ct)) return DeleteResult.InUse;

        context.Categories.Remove(entity);
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
