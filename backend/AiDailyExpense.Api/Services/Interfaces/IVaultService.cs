using AiDailyExpense.Api.DTOs.Vault;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IVaultService
    {
        Task<List<VaultCategoryDto>> GetCategoriesAsync(Guid userId);
        Task<List<VaultItemDto>> GetItemsAsync(Guid userId, string? keyword);
        Task<VaultItemDto> CreateItemAsync(Guid userId, CreateVaultItemRequest request);
        Task<VaultItemDto> UpdateItemAsync(Guid userId, long id, UpdateVaultItemRequest request);
        Task<bool> DeleteItemAsync(Guid userId, long id);
        Task SetPinAsync(Guid userId, SetVaultPinRequest request);
        Task RequestRevealCodeAsync(Guid userId);
        Task<RevealedVaultItemDto> RevealItemAsync(Guid userId, long id, RevealVaultItemRequest request);
    }
}
