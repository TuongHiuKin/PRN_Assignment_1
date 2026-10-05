using AIVES.DAL.Context;
using AIVES.DAL.Entities;
using AIVES.DAL.Repositories.Interfaces;
using AIVES.DAL.Repositories.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DAL.Repositories.Implementations;

public sealed class SystemAccountRepository(AIVESDbContext context) : ISystemAccountRepository
{
    private static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return email.Trim().ToLowerInvariant();
    }

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

    public async Task<bool> ResetPasswordAsync(
        int id,
        string email,
        string expectedHash,
        string newHash,
        CancellationToken ct = default)
    {
        // The old hash acts as a concurrency guard and invalidates every issued reset token.
        return await context.SystemAccounts.Where(x => x.AccountId == id && !x.IsDeleted &&
                x.AccountEmail == email && x.AccountPasswordHash == expectedHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AccountPasswordHash, newHash), ct) == 1;
    }

    public Task<List<SystemAccount>> SearchAsync(
        string? keyword = null,
        byte? role = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = context.SystemAccounts.AsNoTracking();
        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        var term = keyword?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(x => x.AccountName.Contains(term) || x.AccountEmail.Contains(term));
        }

        if (role.HasValue)
        {
            query = query.Where(x => x.AccountRole == role.Value);
        }

        return query.OrderBy(x => x.AccountId).ToListAsync(ct);
    }

    public Task<SystemAccount?> GetByIdAsync(int id, bool includeDeleted = false, CancellationToken ct = default) =>
        context.SystemAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == id && (includeDeleted || !x.IsDeleted), ct);

    public Task<SystemAccount?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        return context.SystemAccounts.AsNoTracking().SingleOrDefaultAsync(x => !x.IsDeleted && x.AccountEmail == normalized, ct);
    }

    public Task<bool> EmailExistsAsync(string email, int? exceptId = null, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        return context.SystemAccounts.AnyAsync(x => x.AccountEmail == normalized && (!exceptId.HasValue || x.AccountId != exceptId.Value), ct);
    }

    public Task<bool> IsReferencedAsync(int id, CancellationToken ct = default) =>
        context.NewsArticles.AnyAsync(x => x.CreatedById == id || x.UpdatedById == id, ct);

    public async Task<int> CreateAsync(AccountCreate input, CancellationToken ct = default)
    {
        var entity = new SystemAccount
        {
            AccountName = input.Name,
            AccountEmail = NormalizeEmail(input.Email),
            AccountPasswordHash = input.PasswordHash,
            AccountRole = input.Role
        };
        context.SystemAccounts.Add(entity);
        await SaveChangesSafeAsync(ct);
        return entity.AccountId;
    }

    public async Task<bool> UpdateAsync(AccountUpdate input, CancellationToken ct = default)
    {
        var email = NormalizeEmail(input.Email);
        var entity = await context.SystemAccounts.SingleOrDefaultAsync(x => x.AccountId == input.Id && !x.IsDeleted, ct);
        if (entity is null) return false;
        entity.AccountName = input.Name;
        entity.AccountEmail = email;
        entity.AccountRole = input.Role;
        if (input.NewPasswordHash is not null) entity.AccountPasswordHash = input.NewPasswordHash;
        await SaveChangesSafeAsync(ct);
        return true;
    }

    public async Task<bool> UpdateProfileAsync(ProfileUpdate input, CancellationToken ct = default)
    {
        var email = NormalizeEmail(input.Email);
        var entity = await context.SystemAccounts.SingleOrDefaultAsync(x => x.AccountId == input.Id && !x.IsDeleted, ct);
        if (entity is null) return false;
        entity.AccountName = input.Name;
        entity.AccountEmail = email;
        if (input.NewPasswordHash is not null) entity.AccountPasswordHash = input.NewPasswordHash;
        // This contract cannot change role, identity, or deletion state.
        await SaveChangesSafeAsync(ct);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await context.SystemAccounts.SingleOrDefaultAsync(x => x.AccountId == id, ct);
        if (entity is null) return false;
        entity.IsDeleted = true;
        await SaveChangesSafeAsync(ct);
        return true;
    }

    public async Task<DeleteResult> HardDeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await context.SystemAccounts.SingleOrDefaultAsync(x => x.AccountId == id, ct);
        if (entity is null) return DeleteResult.NotFound;
        if (await IsReferencedAsync(id, ct)) return DeleteResult.InUse;

        context.SystemAccounts.Remove(entity);
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
