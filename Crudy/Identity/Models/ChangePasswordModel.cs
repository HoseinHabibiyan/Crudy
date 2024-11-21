namespace Crudy.Identity.Models
{
    public record ChangePasswordModel(string Password, string NewPassword, string RepeatPassword);
}
