using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Vault;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services;
using AiDailyExpense.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiDailyExpense.Tests
{
    public class DummyEmailService : IEmailService
    {
        public Task SendCodeAsync(string toEmail, string subject, string code) => Task.CompletedTask;
    }

    public class VaultSecurityAndMigrationTests
    {
        private static readonly byte[] ValidNewKeyBytes = new byte[32]
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        };
        private static readonly string ValidNewKeyBase64 = Convert.ToBase64String(ValidNewKeyBytes);

        private static AppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        #region SecurityConfigValidator Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("short_key")]
        [InlineData("A_SECURE_RANDOM_SECRET_KEY_FOR_JWT_SIGNING_MIN_32_CHARACTERS")]
        [InlineData("CHANGE_THIS_SECRET_KEY_TO_A_LONG_RANDOM_VALUE")]
        [InlineData("YOUR_JWT_SECRET_KEY_12345678901234567890")]
        public void ValidateProductionConfiguration_ThrowsWhenJwtKeyIsInvalid(string? jwtKey)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", jwtKey },
                    { "Vault:MasterKey", ValidNewKeyBase64 }
                })
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SecurityConfigValidator.ValidateProductionConfiguration(config));
            Assert.Contains("Jwt:Key", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("short_key")]
        [InlineData("A_STRONG_MASTER_KEY_FOR_VAULT_AES_GCM_ENCRYPTION_MIN_32_CHARS")]
        [InlineData("AiDailyExpense_DefaultSecureVaultEncryptionMasterKey_2026!")]
        [InlineData("YOUR_VAULT_KEY_1234567890123456789012345")]
        public void ValidateProductionConfiguration_ThrowsWhenVaultKeyIsInvalidPlaceholder(string? vaultKey)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", "A_REALLY_STRONG_RANDOM_SECRET_KEY_FOR_PRODUCTION_JWT_TESTING_PURPOSES_123456" },
                    { "Vault:MasterKey", vaultKey }
                })
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SecurityConfigValidator.ValidateProductionConfiguration(config));
            Assert.Contains("Vault:MasterKey", ex.Message);
        }

        [Fact]
        public void ValidateProductionConfiguration_ThrowsWhenVaultKeyIsNot32BytesBase64()
        {
            // 24 bytes in Base64 gives a 32-character string (passes the length >= 32 check, but key is 24 bytes instead of 32 bytes)
            var not32BytesBase64 = Convert.ToBase64String(new byte[24]);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", "A_REALLY_STRONG_RANDOM_SECRET_KEY_FOR_PRODUCTION_JWT_TESTING_PURPOSES_123456" },
                    { "Vault:MasterKey", not32BytesBase64 }
                })
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                SecurityConfigValidator.ValidateProductionConfiguration(config));
            Assert.Contains("32 bytes", ex.Message);
        }

        [Fact]
        public void ValidateProductionConfiguration_SucceedsWithValidKeys()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", "A_REALLY_STRONG_RANDOM_SECRET_KEY_FOR_PRODUCTION_JWT_TESTING_PURPOSES_123456" },
                    { "Vault:MasterKey", ValidNewKeyBase64 }
                })
                .Build();

            // Should not throw
            SecurityConfigValidator.ValidateProductionConfiguration(config);
        }

        #endregion

        #region VaultService Encryption & Decryption Tests

        [Fact]
        public async Task VaultService_CreateAndReveal_RoundtripSucceeds()
        {
            using var context = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var user = new User
            {
                UserId = userId,
                FullName = "Tester",
                Email = "test@example.com",
                VaultPinHash = BCrypt.Net.BCrypt.HashPassword("123456")
            };
            context.Users.Add(user);
            context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                Email = user.Email,
                Purpose = "VaultAccess",
                CodeHash = BCrypt.Net.BCrypt.HashPassword("654321"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Vault:MasterKey", ValidNewKeyBase64 }
                })
                .Build();

            var service = new VaultService(context, new DummyEmailService(), config);

            var created = await service.CreateItemAsync(userId, new CreateVaultItemRequest
            {
                Title = "Test Email Account",
                LoginUsername = "myuser",
                Password = "SuperSecretPassword99!",
                Notes = "Secret notes here",
                Favorite = true
            });

            var revealed = await service.RevealItemAsync(userId, created.VaultItemId, new RevealVaultItemRequest
            {
                Pin = "123456",
                EmailCode = "654321"
            });

            Assert.Equal("SuperSecretPassword99!", revealed.Password);
            Assert.Equal("Secret notes here", revealed.Notes);
        }

        [Fact]
        public async Task VaultService_RevealItem_SucceedsWithLegacyKey()
        {
            using var context = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var user = new User
            {
                UserId = userId,
                FullName = "Legacy User",
                Email = "legacy@example.com",
                VaultPinHash = BCrypt.Net.BCrypt.HashPassword("123456")
            };
            context.Users.Add(user);
            context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                Email = user.Email,
                Purpose = "VaultAccess",
                CodeHash = BCrypt.Net.BCrypt.HashPassword("654321"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            });

            // Encrypt using old mechanism: SHA256(legacySecret)
            const string legacySecret = "OldLegacySecretPassphrase2025!";
            var oldKey = SHA256.HashData(Encoding.UTF8.GetBytes(legacySecret));
            var plainPwd = "OldLegacyEncryptedPassword123";
            var plainBytes = Encoding.UTF8.GetBytes(plainPwd);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var cipher = new byte[plainBytes.Length];
            using (var aes = new AesGcm(oldKey, 16))
            {
                aes.Encrypt(nonce, plainBytes, cipher, tag);
            }

            var item = new VaultItem
            {
                UserId = userId,
                Title = "Legacy Bank",
                EncryptedPassword = cipher,
                PasswordNonce = nonce,
                PasswordAuthTag = tag,
                CreatedAt = DateTime.UtcNow
            };
            context.VaultItems.Add(item);
            await context.SaveChangesAsync();

            // Service configured with new MasterKey and legacy secrets containing old secret
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Vault:MasterKey", ValidNewKeyBase64 },
                    { "Vault:LegacySecrets", $"SomeOtherSecret,{legacySecret}" }
                })
                .Build();

            var service = new VaultService(context, new DummyEmailService(), config);

            var revealed = await service.RevealItemAsync(userId, item.VaultItemId, new RevealVaultItemRequest
            {
                Pin = "123456",
                EmailCode = "654321"
            });

            Assert.Equal(plainPwd, revealed.Password);
        }

        [Fact]
        public async Task VaultService_RevealItem_ThrowsVaultDecryptionExceptionWithId_WhenTampered()
        {
            using var context = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();
            var user = new User
            {
                UserId = userId,
                FullName = "Tamper Test User",
                Email = "tamper@example.com",
                VaultPinHash = BCrypt.Net.BCrypt.HashPassword("123456")
            };
            context.Users.Add(user);
            context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                Email = user.Email,
                Purpose = "VaultAccess",
                CodeHash = BCrypt.Net.BCrypt.HashPassword("654321"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Vault:MasterKey", ValidNewKeyBase64 }
                })
                .Build();

            var service = new VaultService(context, new DummyEmailService(), config);
            var created = await service.CreateItemAsync(userId, new CreateVaultItemRequest
            {
                Title = "Tamper Target",
                Password = "ValidPassword123"
            });

            // Tamper 1 byte in the database
            var dbItem = await context.VaultItems.FindAsync(created.VaultItemId);
            Assert.NotNull(dbItem);
            dbItem.EncryptedPassword[0] ^= 0xFF;
            await context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<VaultDecryptionException>(() =>
                service.RevealItemAsync(userId, created.VaultItemId, new RevealVaultItemRequest
                {
                    Pin = "123456",
                    EmailCode = "654321"
                }));

            Assert.Equal(created.VaultItemId, ex.VaultItemId);
            Assert.Contains("Không thể giải mã", ex.Message);
        }

        [Fact]
        public async Task VaultService_ReEncryptAllAsync_MigratesLegacyItemsToNewMasterKey()
        {
            using var context = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();

            const string legacySecret1 = "LegacySecretA_2025";
            const string legacySecret2 = "LegacySecretB_2025";

            // Encrypt item 1 with legacy key 1
            var key1 = SHA256.HashData(Encoding.UTF8.GetBytes(legacySecret1));
            var (c1, n1, t1) = EncryptHelper("PasswordOne", key1);
            var item1 = new VaultItem { UserId = userId, Title = "Item 1", EncryptedPassword = c1, PasswordNonce = n1, PasswordAuthTag = t1 };

            // Encrypt item 2 with legacy key 2
            var key2 = SHA256.HashData(Encoding.UTF8.GetBytes(legacySecret2));
            var (c2, n2, t2) = EncryptHelper("PasswordTwo", key2);
            var item2 = new VaultItem { UserId = userId, Title = "Item 2", EncryptedPassword = c2, PasswordNonce = n2, PasswordAuthTag = t2 };

            // Encrypt item 3 with new master key
            var (c3, n3, t3) = EncryptHelper("PasswordThree", ValidNewKeyBytes);
            var item3 = new VaultItem { UserId = userId, Title = "Item 3", EncryptedPassword = c3, PasswordNonce = n3, PasswordAuthTag = t3 };

            context.VaultItems.AddRange(item1, item2, item3);
            await context.SaveChangesAsync();

            // Config with new MasterKey and both legacy secrets
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Vault:MasterKey", ValidNewKeyBase64 },
                    { "Vault:LegacySecrets", $"{legacySecret1};{legacySecret2}" }
                })
                .Build();

            var service = new VaultService(context, new DummyEmailService(), config);
            var result = await service.ReEncryptAllAsync();

            Assert.Equal(3, result.TotalItems);
            Assert.Equal(3, result.SucceededCount);
            Assert.Equal(0, result.FailedCount);

            // Now, simulate dropping legacy secrets completely!
            var pureConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Vault:MasterKey", ValidNewKeyBase64 },
                    { "Vault:LegacySecrets", "" }
                })
                .Build();

            var user = new User
            {
                UserId = userId,
                FullName = "ReEncrypt User",
                Email = "reencrypt@example.com",
                VaultPinHash = BCrypt.Net.BCrypt.HashPassword("112233")
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var pureService = new VaultService(context, new DummyEmailService(), pureConfig);

            // Verify item 1 can be decrypted with ONLY the new master key
            context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                Email = user.Email,
                Purpose = "VaultAccess",
                CodeHash = BCrypt.Net.BCrypt.HashPassword("101010"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var r1 = await pureService.RevealItemAsync(userId, item1.VaultItemId, new RevealVaultItemRequest
            {
                Pin = "112233",
                EmailCode = "101010"
            });
            Assert.Equal("PasswordOne", r1.Password);

            // Verify item 2 can be decrypted with ONLY the new master key
            context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                Email = user.Email,
                Purpose = "VaultAccess",
                CodeHash = BCrypt.Net.BCrypt.HashPassword("202020"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var r2 = await pureService.RevealItemAsync(userId, item2.VaultItemId, new RevealVaultItemRequest
            {
                Pin = "112233",
                EmailCode = "202020"
            });
            Assert.Equal("PasswordTwo", r2.Password);
        }

        [Fact]
        public async Task VaultService_ReEncryptAllAsync_HandlesCorruptedItemsGracefully()
        {
            using var context = CreateInMemoryDbContext();
            var userId = Guid.NewGuid();

            var (c1, n1, t1) = EncryptHelper("PasswordGood", ValidNewKeyBytes);
            var itemGood = new VaultItem { UserId = userId, Title = "Good Item", EncryptedPassword = c1, PasswordNonce = n1, PasswordAuthTag = t1 };

            var itemBad = new VaultItem
            {
                UserId = userId,
                Title = "Corrupted Item",
                EncryptedPassword = new byte[] { 9, 9, 9 },
                PasswordNonce = new byte[12],
                PasswordAuthTag = new byte[16]
            };

            context.VaultItems.AddRange(itemGood, itemBad);
            await context.SaveChangesAsync();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Vault:MasterKey", ValidNewKeyBase64 }
                })
                .Build();

            var service = new VaultService(context, new DummyEmailService(), config);
            var result = await service.ReEncryptAllAsync();

            Assert.Equal(2, result.TotalItems);
            Assert.Equal(1, result.SucceededCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Contains(itemBad.VaultItemId, result.FailedItemIds);
        }

        private static (byte[] Cipher, byte[] Nonce, byte[] Tag) EncryptHelper(string plainText, byte[] key)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var cipher = new byte[plainBytes.Length];
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plainBytes, cipher, tag);
            return (cipher, nonce, tag);
        }

        #endregion
    }
}
