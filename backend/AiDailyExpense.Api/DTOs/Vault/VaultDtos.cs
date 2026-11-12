using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Vault
{
    public class VaultCategoryDto
    {
        public int VaultCategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Color { get; set; }
    }

    public class VaultItemDto
    {
        public long VaultItemId { get; set; }
        public int? VaultCategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? WebsiteUrl { get; set; }
        public string? LoginUsername { get; set; }
        public string? LoginEmail { get; set; }
        public bool Favorite { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateVaultItemRequest
    {
        public int? VaultCategoryId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? WebsiteUrl { get; set; }

        [StringLength(255)]
        public string? LoginUsername { get; set; }

        [StringLength(255)]
        public string? LoginEmail { get; set; }

        [Required]
        public string Password { get; set; } = string.Empty;

        public string? Notes { get; set; }
        public bool Favorite { get; set; }
    }

    public class UpdateVaultItemRequest : CreateVaultItemRequest
    {
    }

    public class SetVaultPinRequest
    {
        public string? OldPin { get; set; }

        [Required]
        public string Pin { get; set; } = string.Empty;

        [Compare("Pin", ErrorMessage = "Xác nhận PIN không khớp.")]
        public string ConfirmPin { get; set; } = string.Empty;
    }

    public class RevealVaultItemRequest
    {
        [Required]
        [RegularExpression(@"^\d{6}$")]
        public string EmailCode { get; set; } = string.Empty;

        [Required]
        public string Pin { get; set; } = string.Empty;
    }

    public class RevealedVaultItemDto
    {
        public long VaultItemId { get; set; }
        public string Password { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
