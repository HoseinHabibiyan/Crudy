﻿namespace Crudy.Documents
{
    public class TokenDocument
    {
        public string Id { get; set; } = default!;
        public required string Token { get; set; }
        public string? UserId { get; set; }
        public required string IPAddress { get; set; }
        public DateTimeOffset? ExpirationDate { get; set; }
        public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.Now;
        public int ResourceCount { get; set; }

    }
}
