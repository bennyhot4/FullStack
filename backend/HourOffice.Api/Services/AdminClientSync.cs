using HourOffice.Api.Data;
using MongoDB.Driver;

namespace HourOffice.Api.Services;

/// <summary>Keeps a copy of every registered user (with a completed profile) in each admin's own client list.
/// Each admin's copy is a normal client of that admin, marked with <see cref="Client.SourceUserId"/>; admins still
/// never see other users' clients. Failures are logged (ids only) and never fail the caller's request.</summary>
public sealed class AdminClientSync(MongoContext db, TimeProvider clock, ILogger<AdminClientSync> log)
{
    /// <summary>A user just completed their profile: add them to every active admin except themself.</summary>
    public async Task AddUserToAdminsAsync(User user, CancellationToken ct)
    {
        if (!HasProfile(user)) return;
        try
        {
            var admins = await db.Users.Find(u => u.Role == Roles.Admin && u.Status == "active" && u.Id != user.Id)
                .Project(u => u.Id).ToListAsync(ct);
            foreach (var adminId in admins) await TryAddAsync(adminId, user, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Adding user {UserId} to the admins' clients failed", user.Id);
        }
    }

    /// <summary>The user changed their profile: update every admin's copy (name, first phone, first email, consent).
    /// Fields the admin added (second phone/email, language, notes) are kept. Read-modify-write per copy (one per
    /// admin): phones and emails may be encrypted at rest, so a positional "phones.0" update is not possible.</summary>
    public async Task UpdateUserInAdminsAsync(User user, CancellationToken ct)
    {
        if (!HasProfile(user)) return;
        try
        {
            var copies = await db.Clients.Find(c => c.SourceUserId == user.Id).ToListAsync(ct);
            foreach (var c in copies)
            {
                c.FirstName = user.FirstName;
                c.LastName = user.LastName;
                if (c.Phones.Count == 0) c.Phones.Add(user.PhoneNumber!); else c.Phones[0] = user.PhoneNumber!;
                if (c.Emails.Count == 0) c.Emails.Add(user.Email); else c.Emails[0] = user.Email;
                c.WhatsAppConsent = user.MarketingConsent;
                c.WhatsAppConsentAt = user.MarketingConsent ? user.MarketingConsentAt : null;
                c.UpdatedAt = clock.GetUtcNow().UtcDateTime;
                await db.Clients.ReplaceOneAsync(x => x.Id == c.Id, c, cancellationToken: ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Updating the admins' copies of user {UserId} failed", user.Id);
        }
    }

    /// <summary>Someone just became an active admin: add every user with a completed profile to their list.
    /// Users already there are skipped, so it is safe to run again.</summary>
    public async Task AddAllUsersToAdminAsync(User admin, CancellationToken ct)
    {
        if (admin.Role != Roles.Admin || admin.Status != "active") return;
        try
        {
            var have = (await db.Clients.Find(c => c.UserId == admin.Id && c.SourceUserId != null)
                .Project(c => c.SourceUserId).ToListAsync(ct)).ToHashSet();
            var users = await db.Users.Find(u => u.Id != admin.Id && u.PhoneNumber != null && u.PhoneNumber != "")
                .SortBy(u => u.CreatedAt).ToListAsync(ct);
            foreach (var user in users.Where(u => !have.Contains(u.Id)))
                if (!await TryAddAsync(admin.Id, user, ct)) break;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Adding all users to admin {AdminId}'s clients failed", admin.Id);
        }
    }

    /// <summary>Startup pass, run once per database: fills every active admin's list with the admins and users
    /// that existed before this feature. A marker in siteSettings stops it from running again, so a copy an admin
    /// deleted is not brought back on the next restart.</summary>
    public async Task FillAllAdminsOnceAsync(CancellationToken ct = default)
    {
        if (await db.Settings.Find(s => s.Key == SettingKeys.AdminClientsFilled).AnyAsync(ct)) return;
        var admins = await db.Users.Find(u => u.Role == Roles.Admin && u.Status == "active").ToListAsync(ct);
        foreach (var admin in admins) await AddAllUsersToAdminAsync(admin, ct);
        try
        {
            await db.Settings.InsertOneAsync(new SiteSetting
            {
                Key = SettingKeys.AdminClientsFilled, Value = clock.GetUtcNow().UtcDateTime.ToString("O"),
            }, cancellationToken: ct);
        }
        catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
        {
            // Another instance finished first.
        }
    }

    /// <summary>Adds one copy. Returns false only when the admin's list is full.</summary>
    private async Task<bool> TryAddAsync(string adminId, User user, CancellationToken ct)
    {
        if (await db.Clients.CountDocumentsAsync(c => c.UserId == adminId, cancellationToken: ct) >= Client.MaxPerUser)
        {
            log.LogWarning("Admin {AdminId} has reached the client limit; user {UserId} was not added", adminId, user.Id);
            return false;
        }
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            await db.Clients.InsertOneAsync(new Client
            {
                UserId = adminId,
                SourceUserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phones = new() { user.PhoneNumber! },
                Emails = new() { user.Email },
                Language = "he",
                WhatsAppConsent = user.MarketingConsent,
                WhatsAppConsentAt = user.MarketingConsent ? user.MarketingConsentAt : null,
                CreatedAt = now,
                UpdatedAt = now,
            }, cancellationToken: ct);
        }
        catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))
        {
            // Another request added this copy first.
        }
        return true;
    }

    private static bool HasProfile(User u) => !string.IsNullOrWhiteSpace(u.PhoneNumber);
}
