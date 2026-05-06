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
    Task<ServiceResult<UserAccount>> RegisterAsync(string email, string displayName, string password);
    Task<ServiceResult<UserAccount>> LoginAsync(string email, string password);
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
                               "mongodb://localhost:27017";
        var databaseName = Environment.GetEnvironmentVariable("SNEAKER_MONGODB_DATABASE") ??
                           "SneakerCollection";

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _users = database.GetCollection<UserAccount>("users");
    }

    public async Task<ServiceResult<UserAccount>> RegisterAsync(string email, string displayName, string password)
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

            var account = new UserAccount
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                DisplayName = displayName,
                PasswordHash = PasswordHasher.Hash(password),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _users.InsertOneAsync(account);
            return ServiceResult<UserAccount>.Ok(account, $"Logged in: {account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable, using local BSON fallback.");
            return await _fallback.RegisterAsync(email, displayName, password);
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

    public async Task<ServiceResult<UserAccount>> RegisterAsync(string email, string displayName, string password)
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
                ["passwordHash"] = user.PasswordHash,
                ["createdAtUtcTicks"] = user.CreatedAtUtc.Ticks
            }))
        };

        await File.WriteAllBytesAsync(_filePath, document.ToBson());
    }
}
