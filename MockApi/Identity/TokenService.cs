using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MockApi.Identity.Models;

namespace MockApi.Identity;

public class TokenService 
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GenerateToken()
    {
        var randomNumber = new byte[32];
        var randomNumberGenerator = RandomNumberGenerator.Create();
        randomNumberGenerator.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public AuthTokenModel Authenticate(Claim[] claims, DateTime expire)
    {
        var secretKey = _configuration["Jwt:ClientSecret"];
        var key = Encoding.ASCII.GetBytes(secretKey);
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new(claims),
            Expires = expire,
            SigningCredentials = new(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var refreshToken = GenerateToken();
        return new()
        {
            Token = tokenHandler.WriteToken(token),
            RefreshToken = refreshToken
        };
    }

    public AuthTokenModel RefreshAuthenticate(AuthTokenModel tokenItem, Claim[] claims, DateTime expire)
    {
        var secretKey = _configuration["Jwt:SecretKey"];
        var key = Encoding.ASCII.GetBytes(secretKey);
        var tokenHandler = new JwtSecurityTokenHandler();
        SecurityToken validateToken;
        var pricipal = tokenHandler.ValidateToken(tokenItem.Token, new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        }, out validateToken);
        var jwtToken = validateToken as JwtSecurityToken;
        if (jwtToken == null || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256)) throw new("Invalid Token!");
        return Authenticate(claims, expire);
    }
}