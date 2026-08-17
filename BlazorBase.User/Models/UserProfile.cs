namespace BlazorBase.User.Models;

public class UserProfile
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ThemePreference { get; set; } = "System";
    public string Language { get; set; } = "en";
    public string Role { get; set; } = string.Empty;
}
