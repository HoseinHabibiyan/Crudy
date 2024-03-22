namespace MockApi.Identity.Models
{
    public record ChangePasswordModel(string Email, string Password, string NewPassword, string RepeatPassword);
}
