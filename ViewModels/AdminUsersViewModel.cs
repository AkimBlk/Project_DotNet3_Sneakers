using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyProjectBase.Models;
using MyProjectBase.Repositories;
using MyProjectBase.Services;

namespace MyProjectBase.ViewModels;

public partial class AdminUsersViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;
    private readonly IDialogService _dialogService;
    private readonly IAppLogger _logger;
    private readonly string _currentUserId;

    public ObservableCollection<UserAccount> Users { get; } = [];
    public IReadOnlyList<UserRole> Roles { get; } = [UserRole.User, UserRole.Admin];

    [ObservableProperty] private UserAccount? _selectedUser;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private UserRole _role = UserRole.User;
    [ObservableProperty] private string _message = "Load users to manage accounts.";
    [ObservableProperty] private bool _isBusy;

    public AdminUsersViewModel()
        : this(new BsonUserRepository(new AppLogger()), new DialogService(), new AppLogger(), string.Empty)
    {
    }

    public AdminUsersViewModel(
        IUserRepository userRepository,
        IDialogService dialogService,
        IAppLogger logger,
        string currentUserId)
    {
        _userRepository = userRepository;
        _dialogService = dialogService;
        _logger = logger;
        _currentUserId = currentUserId;
        _ = LoadUsersAsync();
    }

    partial void OnSelectedUserChanged(UserAccount? value)
    {
        if (value == null)
            return;

        FirstName = value.FirstName;
        LastName = value.LastName;
        Email = value.Email;
        DisplayName = value.DisplayName;
        Role = value.Role;
        Password = string.Empty;
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _userRepository.GetUsersAsync();
            Users.Clear();

            if (result.Value != null)
            {
                foreach (var user in result.Value)
                    Users.Add(user);
            }

            Message = result.Message;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Admin users load failed.");
            Message = "Unable to load users.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewUser()
    {
        SelectedUser = null;
        FirstName = string.Empty;
        LastName = string.Empty;
        Email = string.Empty;
        DisplayName = string.Empty;
        Password = string.Empty;
        Role = UserRole.User;
        Message = "Creating a new user.";
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        var user = SelectedUser ?? new UserAccount();
        user.FirstName = FirstName;
        user.LastName = LastName;
        user.Email = Email;
        user.DisplayName = DisplayName;
        user.Role = Role;
        user.NormalizeNames();

        if (string.IsNullOrWhiteSpace(user.PasswordHash) && string.IsNullOrWhiteSpace(Password))
        {
            Message = "Password is required for new users.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _userRepository.SaveUserAsync(user, Password);
            if (!result.Success || result.Value == null)
            {
                Message = result.Message;
                return;
            }

            var existingIndex = Users.ToList().FindIndex(existing => existing.Id == result.Value.Id);
            if (existingIndex >= 0)
                Users[existingIndex] = result.Value;
            else
                Users.Add(result.Value);

            SelectedUser = result.Value;
            Password = string.Empty;
            Message = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteUserAsync()
    {
        if (SelectedUser == null)
        {
            Message = "Select a user to delete.";
            return;
        }

        if (SelectedUser.Id == _currentUserId)
        {
            Message = "You cannot delete the active admin account.";
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync($"Delete {SelectedUser.Email}?");
        if (!confirmed)
        {
            Message = "Delete canceled.";
            return;
        }

        var id = SelectedUser.Id;
        IsBusy = true;
        try
        {
            var result = await _userRepository.DeleteUserAsync(id);
            if (result.Success)
            {
                var user = Users.FirstOrDefault(existing => existing.Id == id);
                if (user != null)
                    Users.Remove(user);
                NewUser();
            }

            Message = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
