using AIVES.DAL.Context;
using AIVES.DAL.Entities;
using AIVES.DAL.Repositories.Interfaces;
using AIVES.DAL.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DAL.Repositories.Implementations;

public sealed class NewsArticleRepository(AIVESDbContext context) : INewsArticleRepository
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

    private IQueryable<NewsArticle> ReadQuery() => context.NewsArticles.AsNoTracking()
        .Include(x => x.Category).Include(x => x.CreatedBy).Include(x => x.UpdatedBy)
        .Include(x => x.NewsTags).ThenInclude(x => x.Tag);

    private static IQueryable<NewsArticle> ApplySearch(IQueryable<NewsArticle> query, string? keyword)
    {
        var term = keyword?.Trim();
        return string.IsNullOrEmpty(term) ? query : query.Where(x => x.NewsTitle.Contains(term) || x.NewsContent.Contains(term));
    }

    private static Task<List<NewsArticle>> ListAsync(IQueryable<NewsArticle> query, CancellationToken ct) =>
        query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.NewsArticleId).ToListAsync(ct);

    public Task<List<NewsArticle>> SearchAsync(string? keyword = null, CancellationToken ct = default) =>
        ListAsync(ApplySearch(ReadQuery(), keyword), ct);

    public Task<NewsArticle?> GetByIdAsync(int id, bool activeOnly = false, CancellationToken ct = default) =>
        ReadQuery().SingleOrDefaultAsync(x => x.NewsArticleId == id && (!activeOnly || x.NewsStatus == 1), ct);

    public Task<List<NewsArticle>> GetActiveAsync(string? keyword = null, CancellationToken ct = default) =>
        ListAsync(ApplySearch(ReadQuery().Where(x => x.NewsStatus == 1), keyword), ct);

    public Task<List<NewsArticle>> GetByCreatorAsync(int creatorId, string? keyword = null, CancellationToken ct = default) =>
        ListAsync(ApplySearch(ReadQuery().Where(x => x.CreatedById == creatorId), keyword), ct);

    public Task<List<NewsArticle>> GetByCreatedDateRangeAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken ct = default)
    {
        if (startUtc.Kind != DateTimeKind.Utc || endExclusiveUtc.Kind != DateTimeKind.Utc || startUtc >= endExclusiveUtc)
            throw new ArgumentException("Expected a non-empty UTC half-open date range.");
        return ListAsync(ReadQuery().Where(x => x.CreatedDate >= startUtc && x.CreatedDate < endExclusiveUtc), ct);
    }

    public async Task<int> CreateAsync(NewsCreate input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input.TagIds);
        var entity = new NewsArticle
        {
            NewsTitle = input.Title,
            NewsContent = input.Content,
            CategoryId = input.CategoryId,
            NewsStatus = input.Status,
            CreatedById = input.CreatedById,
            NewsTags = input.TagIds.Distinct().Select(id => new NewsTag { TagId = id }).ToList()
        };
        // SQL supplies CreatedDate. One save atomically persists News and its join rows.
        context.NewsArticles.Add(entity);
        await SaveChangesSafeAsync(ct);
        return entity.NewsArticleId;
    }

    public async Task<bool> UpdateAsync(NewsUpdate input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input.TagIds);
        if (input.ModifiedUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("ModifiedUtc must be UTC.", nameof(input));
        var ids = input.TagIds.ToHashSet();
        var entity = await context.NewsArticles.Include(x => x.NewsTags).SingleOrDefaultAsync(x => x.NewsArticleId == input.Id, ct);
        if (entity is null) return false;
        entity.NewsTitle = input.Title;
        entity.NewsContent = input.Content;
        entity.CategoryId = input.CategoryId;
        entity.NewsStatus = input.Status;
        entity.UpdatedById = input.UpdatedById;
        entity.ModifiedDate = input.ModifiedUtc;
        // Never overwrite CreatedById/CreatedDate. Preserve unchanged link instances.
        var existingIds = entity.NewsTags.Select(x => x.TagId).ToHashSet();
        foreach (var link in entity.NewsTags.Where(x => !ids.Contains(x.TagId)).ToList())
        {
            entity.NewsTags.Remove(link);
            context.NewsTags.Remove(link);
        }
        foreach (var id in ids.Except(existingIds))
            entity.NewsTags.Add(new NewsTag { TagId = id });
        await SaveChangesSafeAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await context.NewsArticles.SingleOrDefaultAsync(x => x.NewsArticleId == id, ct);
        if (entity is null) return false;
        context.NewsArticles.Remove(entity);
        await SaveChangesSafeAsync(ct);
        return true;
    }
}
