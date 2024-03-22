namespace MockApi.Identity.Models
{
    public record RegisterModel(string Email, string Password, string? FirstName = null, string? LastName = null, string? ProfileImageUrl = null);
}
