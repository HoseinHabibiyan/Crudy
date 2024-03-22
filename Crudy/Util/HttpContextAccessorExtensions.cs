using System.Security.Claims;

namespace Crudy.Util
{
    public static class HttpContextExtensions
    {
        public static string? GetUserId(this HttpContext httpContext)
        {
            var claims = httpContext.User.Claims.ToList();
            var userId = claims.Any() ? claims.First(i => i.Type == ClaimTypes.NameIdentifier)?.Value : null;

            return userId;
        }

        public static List<string> GetRoles(this HttpContext httpContext)
        {
            var claims = httpContext.User.Claims;

            return claims.Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();
        }

        public static string? GetUserFullName(this HttpContext httpContext)
        {
            var claims = httpContext.User.Claims;

            return claims.Where(c => c.Type == ClaimTypes.GivenName)
                .Select(c => c.Value)
                .Distinct()
                .FirstOrDefault();
        }

        public static string? GetUserEmail(this HttpContext httpContext)
        {
            var claims = httpContext.User.Claims;

            return claims.Where(c => c.Type == ClaimTypes.Email)
                .Select(c => c.Value)
                .Distinct()
                .FirstOrDefault();
        }

        public static bool IsSuperAdmin(this HttpContext httpContext)
        {
            var roles = GetRoles(httpContext);
            return roles.Contains("SuperAdmin");
        }

        public static bool IsAdmin(this HttpContext httpContext)
        {
            var roles = GetRoles(httpContext);
            return roles.Contains("Admin");
        }
    }
}
