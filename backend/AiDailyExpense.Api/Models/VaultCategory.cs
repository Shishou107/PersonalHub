using System;
using System.Collections.Generic;

namespace AiDailyExpense.Api.Models
{
    public class VaultCategory
    {
        public int VaultCategoryId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public ICollection<VaultItem> VaultItems { get; set; } = new List<VaultItem>();
    }
}
