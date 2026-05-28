using System.Text.RegularExpressions;
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
    // Regex simple pour eviter d'envoyer a MongoDB des adresses clairement invalides.
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private readonly IMongoCollection<UserAccount> _users;
    private readonly IAppLogger _logger;

    public MongoUserRepository(IAppLogger logger)
        : this(logger, new AppConfiguration())
    {
    }

    public MongoUserRepository(IAppLogger logger, AppConfiguration configuration)
    {
        _logger = logger;

        // La connexion MongoDB est centralisee ici : les ViewModels ne connaissent pas les details serveur.
        var settings = MongoClientSettings.FromConnectionString(configuration.MongoConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        settings.SocketTimeout = TimeSpan.FromSeconds(8);

        var client = new MongoClient(settings);
        var database = client.GetDatabase(configuration.MongoDatabaseName);
        _users = database.GetCollection<UserAccount>("users");
    }

    public async Task<ServiceResult<UserAccount>> RegisterAsync(string email, string displayName, string password, UserRole role = UserRole.User)
    {
        // Normalisation avant stockage : une meme adresse ne doit pas exister avec deux casses differentes.
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

            // Le premier compte cree devient admin afin de pouvoir gerer les autres utilisateurs.
            var hasAdmin = await _users.Find(user => user.Role == UserRole.Admin).AnyAsync();
            var account = new UserAccount
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                DisplayName = displayName,
                Role = hasAdmin ? role : UserRole.Admin,
                // Le mot de passe n'est jamais stocke en clair : PasswordHasher ajoute un sel et applique PBKDF2.
                PasswordHash = PasswordHasher.Hash(password),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _users.InsertOneAsync(account);
            return ServiceResult<UserAccount>.Ok(account, $"Logged in: {account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable during registration.");
            return ServiceResult<UserAccount>.Unavailable("MongoDB unavailable. Account creation requires the remote database.");
        }
    }

    public async Task<ServiceResult<UserAccount>> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();

        try
        {
            // Authentification : on recupere le compte puis on compare le hash du mot de passe.
            var account = await _users.Find(user => user.Email == email).FirstOrDefaultAsync();
            if (account == null || !PasswordHasher.Verify(password, account.PasswordHash))
                return ServiceResult<UserAccount>.Fail("Invalid credentials.");

            return ServiceResult<UserAccount>.Ok(account, $"Logged in: {account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable during login.");
            return ServiceResult<UserAccount>.Unavailable("MongoDB unavailable. Login requires the remote database.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<UserAccount>>> GetUsersAsync()
    {
        try
        {
            // Utilise par la page admin pour afficher les comptes existants.
            var users = await _users.Find(_ => true).SortBy(user => user.Email).ToListAsync();
            return ServiceResult<IReadOnlyList<UserAccount>>.Ok(users, $"{users.Count} users loaded.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable while listing users.");
            return ServiceResult<IReadOnlyList<UserAccount>>.Unavailable("MongoDB unavailable. User administration requires the remote database.");
        }
    }

    public async Task<ServiceResult<UserAccount>> SaveUserAsync(UserAccount user, string? newPassword = null)
    {
        try
        {
            // SaveUserAsync sert autant a modifier un compte qu'a en creer un depuis l'administration.
            user.Email = user.Email.Trim().ToLowerInvariant();
            user.NormalizeNames();

            if (!EmailRegex.IsMatch(user.Email))
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

                // Quand l'admin change le mot de passe, on remplace seulement le hash, jamais par le texte brut.
                user.PasswordHash = PasswordHasher.Hash(newPassword);
            }

            // ReplaceOne avec IsUpsert permet de faire create/update avec une seule operation MongoDB.
            await _users.ReplaceOneAsync(existing => existing.Id == user.Id, user, new ReplaceOptions { IsUpsert = true });
            return ServiceResult<UserAccount>.Ok(user, "User saved.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable while saving a user.");
            return ServiceResult<UserAccount>.Unavailable("MongoDB unavailable. User save requires the remote database.");
        }
    }

    public async Task<ServiceResult> DeleteUserAsync(string userId)
    {
        try
        {
            // Suppression definitive du compte utilisateur dans la collection MongoDB "users".
            var result = await _users.DeleteOneAsync(user => user.Id == userId);
            return result.DeletedCount > 0 ? ServiceResult.Ok("User deleted.") : ServiceResult.Fail("User not found.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MongoDB unavailable while deleting a user.");
            return ServiceResult.Unavailable("MongoDB unavailable. User deletion requires the remote database.");
        }
    }

    private static ServiceResult ValidateCredentials(string email, string displayName, string password)
    {
        // Validation minimale avant creation de compte pour donner une erreur claire a l'utilisateur.
        if (!EmailRegex.IsMatch(email))
            return ServiceResult.Fail("Invalid email.");

        if (string.IsNullOrWhiteSpace(displayName))
            return ServiceResult.Fail("Display name is required.");

        return password.Length < 8
            ? ServiceResult.Fail("Password is too short (8 characters minimum).")
            : ServiceResult.Ok();
    }
}
