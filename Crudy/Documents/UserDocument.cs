
namespace Crudy.Documents
{
    public class UserDocument(string id,string email)
    {
        public string Id { get; set; } = id;
        public string Email { get; set; } = email;
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public DateTime JoinDate { get; set; }

        public string? ProfileImageUrl { get; set; }

        public DateTime? LastLogin { get; set; }

        public bool IsSuperUser { get; set; }

        public bool IsDeleted { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
