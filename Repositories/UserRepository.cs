using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MyProjectBase.Models;
using MyProjectBase.Services;
using MyProjectBase.Utilities;

namespace MyProjectBase.Repositories;

public interface IUserRepository
{
    Task<ServiceResult<UserAccount>> RegisterAsync(string email, string displayName, string password, UserRole role = UserRole.User);
    Task<ServiceResult<UserAccount>> LoginAsync(string email, string password);
    Task<ServiceResult<IReadOnlyList<UserAccount>>> GetUsersAsync();
    Task<ServiceResult<UserAccount>> SaveUserAsync(UserAccount user, string? newPassword = null);
    Task<ServiceResult> DeleteUserAsync(string userId);
}

public sealed class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<UserAccount> _users;
    private readonly IUserRepository _fallback;
    private readonly IAppLogger _logger;

    public MongoUserRepository(IAppLogger logger)
    {
        _logger = logger;
        _fallback = new BsonUserRepository(logger);

        var connectionString = Environment.GetEnvironmentVariable("SNEAKER_MONGODB_URI") ??
                               "mongodb://Meeeee:IAmTheBest@185.157.245.38:443/?authSource=admin&tls=true";
        var databaseName = Environment.GetEnvironmentVariable("SNEAKER_MONGODB_DATABASE") ??
                           "SneakerCollection";

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(8);

        var client = new MongoClient(settings);
        var database = client.GetDatabase(databaseName);
        _users = database.GetCollection<UserAccount>("users");
    }

    public async Task<ServiceResult<UserAccount>> RegisterAsync(string email, string displayName, string password, UserRole role = UserRole.User)
    {
        email = email.Trim().ToLowerInvariant();
        displayName = displayName.Trim();

        var validation = ValidateCredentials(email, displayName, password);
        if (!validation.Success)
            return ServiceResult<UserAccount>.Fail(validation.Message);

        try
        {
            var existing = await _users.Find(user => user.Email == email).FirstOrDefaultAsync();
            if (existing != null)
                return ServiceResult<UserAccount>.Fail("This account already exists.");

            var hasAdmin = await _users.Find(user => user.Role == UserRole.Admin).AnyAsync();
            var account = new UserAccount
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                DisplayName = displayName,
                Role = hasAdmin ? role : UserRole.Admin,
                PasswordHash = PasswordHasher.Hash(password),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _users.InsertOneAsync(account);
            return ServiceResult<UserAccount>.Ok(account, $"Logged in: {account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable, using local BSON fallback.");
            return await _fallback.RegisterAsync(email, displayName, password, role);
        }
    }

    public async Task<ServiceResult<UserAccount>> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();

        try
        {
            var account = await _users.Find(user => user.Email == email).FirstOrDefaultAsync();
            if (account == null || !PasswordHasher.Verify(password, account.PasswordHash))
                return ServiceResult<UserAccount>.Fail("Invalid credentials.");

            return ServiceResult<UserAccount>.Ok(account, $"Logged in: {account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable, using local BSON login fallback.");
            return await _fallback.LoginAsync(email, password);
        }
    }

    public async Task<ServiceResult<IReadOnlyList<UserAccount>>> GetUsersAsync()
    {
        try
        {
            var users = await _users.Find(_ => true).SortBy(user => user.Email).ToListAsync();
            return ServiceResult<IReadOnlyList<UserAccount>>.Ok(users, $"{users.Count} users loaded.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable, using local BSON user list fallback.");
            return await _fallback.GetUsersAsync();
        }
    }

    public async Task<ServiceResult<UserAccount>> SaveUserAsync(UserAccount user, string? newPassword = null)
    {
        try
        {
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.NormalizeNames();

            if (!BsonUserRepository.EmailRegex.IsMatch(user.Email))
                return ServiceResult<UserAccount>.Fail("Invalid email.");

            if (string.IsNullOrWhiteSpace(user.DisplayName))
                return ServiceResult<UserAccount>.Fail("Display name is required.");

            if (string.IsNullOrWhiteSpace(user.Id))
            {
                user.Id = Guid.NewGuid().ToString();
                user.CreatedAtUtc = DateTime.UtcNow;
            }

            var duplicate = await _users
                .Find(existing => existing.Id != user.Id && existing.Email == user.Email)
                .FirstOrDefaultAsync();

            if (duplicate != null)
                return ServiceResult<UserAccount>.Fail("This email is already used.");

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword.Length < 8)
                    return ServiceResult<UserAccount>.Fail("Password is too short (8 characters minimum).");

                user.PasswordHash = PasswordHasher.Hash(newPassword);
            }

            await _users.ReplaceOneAsync(existing => existing.Id == user.Id, user, new ReplaceOptions { IsUpsert = true });
            return ServiceResult<UserAccount>.Ok(user, "User saved.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable, using local BSON save fallback.");
            return await _fallback.SaveUserAsync(user, newPassword);
        }
    }

    public async Task<ServiceResult> DeleteUserAsync(string userId)
    {
        try
        {
            var result = await _users.DeleteOneAsync(user => user.Id == userId);
            return result.DeletedCount > 0 ? ServiceResult.Ok("User deleted.") : ServiceResult.Fail("User not found.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable, using local BSON delete fallback.");
            return await _fallback.DeleteUserAsync(userId);
        }
    }

    private static ServiceResult ValidateCredentials(string email, string displayName, string password)
    {
        if (!BsonUserRepository.EmailRegex.IsMatch(email))
            return ServiceResult.Fail("Invalid email.");

        if (string.IsNullOrWhiteSpace(displayName))
            return ServiceResult.Fail("Display name is required.");

        return password.Length < 8
            ? ServiceResult.Fail("Password is too short (8 characters minimum).")
            : ServiceResult.Ok();
    }
}

public sealed class BsonUserRepository : IUserRepository
{
    internal static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private readonly IAppLogger _logger;
    private readonly string _filePath;

    public BsonUserRepository(IAppLogger logger)
    {
        _logger = logger;

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SneakerCollection",
            "MongoLikeStore");

        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "users.bson");
    }

    public async Task<ServiceResult<UserAccount>> RegisterAsync(string email, string displayName, string password, UserRole role = UserRole.User)
    {
        email = email.Trim().ToLowerInvariant();
        displayName = displayName.Trim();

        if (!EmailRegex.IsMatch(email))
            return ServiceResult<UserAccount>.Fail("Invalid email.");

        if (string.IsNullOrWhiteSpace(displayName))
            return ServiceResult<UserAccount>.Fail("Display name is required.");

        if (password.Length < 8)
            return ServiceResult<UserAccount>.Fail("Password is too short (8 characters minimum).");

        try
        {
            var users = await LoadUsersAsync();
            if (users.Any(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return ServiceResult<UserAccount>.Fail("This account already exists.");

            var account = new UserAccount
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                DisplayName = displayName,
                Role = users.Any(user => user.Role == UserRole.Admin) ? role : UserRole.Admin,
                PasswordHash = PasswordHasher.Hash(password)
            };

            users.Add(account);
            await SaveUsersAsync(users);
            return ServiceResult<UserAccount>.Ok(account, $"Logged in: {account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "User account creation error.");
            return ServiceResult<UserAccount>.Fail("Unable to create the user account.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<UserAccount>>> GetUsersAsync()
    {
        try
        {
            var users = await LoadUsersAsync();
            return ServiceResult<IReadOnlyList<UserAccount>>.Ok(
                users.OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase).ToList(),
                $"{users.Count} users loaded.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "User list error.");
            return ServiceResult<IReadOnlyList<UserAccount>>.Fail("Unable to load users.");
        }
    }

    public async Task<ServiceResult<UserAccount>> SaveUserAsync(UserAccount user, string? newPassword = null)
    {
        try
        {
            var users = await LoadUsersAsync();
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.NormalizeNames();

            if (!EmailRegex.IsMatch(user.Email))
                return ServiceResult<UserAccount>.Fail("Invalid email.");

            if (string.IsNullOrWhiteSpace(user.DisplayName))
                return ServiceResult<UserAccount>.Fail("Display name is required.");

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword.Length < 8)
                    return ServiceResult<UserAccount>.Fail("Password is too short (8 characters minimum).");

                user.PasswordHash = PasswordHasher.Hash(newPassword);
            }

            if (string.IsNullOrWhiteSpace(user.Id))
            {
                user.Id = Guid.NewGuid().ToString();
                user.CreatedAtUtc = DateTime.UtcNow;
            }

            if (users.Any(existing => existing.Id != user.Id &&
                                      existing.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
                return ServiceResult<UserAccount>.Fail("This email is already used.");

            var index = users.FindIndex(existing => existing.Id == user.Id);
            if (index >= 0)
                users[index] = user;
            else
                users.Add(user);

            await SaveUsersAsync(users);
            return ServiceResult<UserAccount>.Ok(user, "User saved.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "User save error.");
            return ServiceResult<UserAccount>.Fail("Unable to save the user.");
        }
    }

    public async Task<ServiceResult> DeleteUserAsync(string userId)
    {
        try
        {
            var users = await LoadUsersAsync();
            var removed = users.RemoveAll(user => user.Id == userId);
            if (removed == 0)
                return ServiceResult.Fail("User not found.");

            await SaveUsersAsync(users);
            return ServiceResult.Ok("User deleted.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "User delete error.");
            return ServiceResult.Fail("Unable to delete the user.");
        }
    }

    public async Task<ServiceResult<UserAccount>> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();

        try
        {
            var users = await LoadUsersAsync();
            var account = users.FirstOrDefault(user => user.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (account == null || !PasswordHasher.Verify(password, account.PasswordHash))
                return ServiceResult<UserAccount>.Fail("Invalid credentials.");

            return ServiceResult<UserAccount>.Ok(account, $"Logged in: {account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "User login error.");
            return ServiceResult<UserAccount>.Fail("User login unavailable.");
        }
    }

    private async Task<List<UserAccount>> LoadUsersAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        await using var stream = File.OpenRead(_filePath);
        var document = BsonSerializer.Deserialize<BsonDocument>(stream);

        return document.TryGetValue("users", out var value) && value.IsBsonArray
            ? value.AsBsonArray
                .Select(item => item.AsBsonDocument)
                .Select(documentUser => new UserAccount
                {
                    Id = documentUser.GetValue("id", string.Empty).AsString,
                    Email = documentUser.GetValue("email", string.Empty).AsString,
                    DisplayName = documentUser.GetValue("displayName", string.Empty).AsString,
                    FirstName = documentUser.GetValue("firstName", string.Empty).AsString,
                    LastName = documentUser.GetValue("lastName", string.Empty).AsString,
                    Role = Enum.TryParse<UserRole>(documentUser.GetValue("role", nameof(UserRole.User)).AsString, true, out var role)
                        ? role
                        : UserRole.User,
                    PasswordHash = documentUser.GetValue("passwordHash", string.Empty).AsString,
                    CreatedAtUtc = new DateTime(documentUser.GetValue("createdAtUtcTicks", DateTime.UtcNow.Ticks).ToInt64(), DateTimeKind.Utc)
                })
                .ToList()
            : [];
    }

    private async Task SaveUsersAsync(IEnumerable<UserAccount> users)
    {
        var document = new BsonDocument
        {
            ["users"] = new BsonArray(users.Select(user => new BsonDocument
            {
                ["id"] = user.Id,
                ["email"] = user.Email,
                ["displayName"] = user.DisplayName,
                ["firstName"] = user.FirstName,
                ["lastName"] = user.LastName,
                ["role"] = user.Role.ToString(),
                ["passwordHash"] = user.PasswordHash,
                ["createdAtUtcTicks"] = user.CreatedAtUtc.Ticks
            }))
        };

        await File.WriteAllBytesAsync(_filePath, document.ToBson());
    }
}
