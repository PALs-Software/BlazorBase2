using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

public class UpdateUserSettingsRequest
{
    [Required]
    [RegularExpression("^(System|Light|Dark)$")]
    public string ThemePreference { get; set; } = "System";

    [Required]
    [RegularExpression("^(en|de)$")]
    public string Language { get; set; } = "en";
}
