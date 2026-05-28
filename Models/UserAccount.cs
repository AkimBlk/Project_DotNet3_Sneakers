namespace MyProjectBase.Models;

public enum UserRole
{
    // User voit uniquement sa collection privee ; Admin a aussi acces a la page d'administration.
    User,
    Admin
}

public class UserAccount
{
    // Modele stocke dans MongoDB pour representer un compte utilisateur.
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsAdmin => Role == UserRole.Admin;

    // Cle utilisee par le service JSON pour separer les collections : admin ou id utilisateur.
    public string CollectionKey => IsAdmin ? "admin" : Id;

    public void NormalizeNames()
    {
        // Complete DisplayName avec prenom/nom si l'utilisateur ne l'a pas renseigne.
        FirstName = FirstName.Trim();
        LastName = LastName.Trim();
        DisplayName = string.IsNullOrWhiteSpace(DisplayName)
            ? $"{FirstName} {LastName}".Trim()
            : DisplayName.Trim();
    }
}
