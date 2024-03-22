namespace Crudy.Identity.Models;

public class AuthTokenModel
{
    public string Token { get; set; }
    public string RefreshToken { get; set; }
}