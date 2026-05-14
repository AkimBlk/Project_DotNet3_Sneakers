namespace MyProjectBase.Models;

public enum UserRole
{
    User,
    Admin
}

public class UserAccount
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsAdmin => Role == UserRole.Admin;
    public string CollectionKey => IsAdmin ? "admin" : Id;

    public void NormalizeNames()
    {
        FirstName = FirstName.Trim();
        LastName = LastName.Trim();
        DisplayName = string.IsNullOrWhiteSpace(DisplayName)
            ? $"{FirstName} {LastName}".Trim()
            : DisplayName.Trim();
    }
}
