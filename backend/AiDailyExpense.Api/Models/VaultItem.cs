using System;

namespace AiDailyExpense.Api.Models
{
    public class VaultItem
    {
        public long VaultItemId { get; set; }
        public Guid UserId { get; set; }
        public int? VaultCategoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? WebsiteUrl { get; set; }
        public string? LoginUsername { get; set; }
        public string? LoginEmail { get; set; }
        public byte[] EncryptedPassword { get; set; } = Array.Empty<byte>();
        public byte[] PasswordNonce { get; set; } = Array.Empty<byte>();
        public byte[]? PasswordAuthTag { get; set; }
        public byte[]? EncryptedNotes { get; set; }
        public byte[]? NotesNonce { get; set; }
        public byte[]? NotesAuthTag { get; set; }
        public bool Favorite { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public User User { get; set; } = null!;
        public VaultCategory? VaultCategory { get; set; }
    }
}
