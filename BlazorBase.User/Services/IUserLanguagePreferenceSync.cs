namespace BlazorBase.User.Services;

/// <summary>
/// Stores a language choice on the signed-in user's account, so the next start (which resolves the language from
/// the account's <c>language</c> claim) and other devices use it.
/// </summary>
public interface IUserLanguagePreferenceSync
{
    Task SaveAsync(string language);
}
