using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiDailyExpense.Api.Helpers
{
    public static class SecurityConfigValidator
    {
        // Publicly known placeholders that must NEVER be used in production
        private static readonly HashSet<string> KnownInsecurePlaceholders = new(StringComparer.OrdinalIgnoreCase)
        {
            "A_SECURE_RANDOM_SECRET_KEY_FOR_JWT_SIGNING_MIN_32_CHARACTERS",
            "A_STRONG_MASTER_KEY_FOR_VAULT_AES_GCM_ENCRYPTION_MIN_32_CHARS",
            "CHANGE_THIS_SECRET_KEY_TO_A_LONG_RANDOM_VALUE",
            "CHANGE_THIS_SECRET_KEY_TO_A_LONG_RANDOM_VALUE_THAT_HAS_AT_LEAST_32_CHARACTERS_FOR_HMACSHA256_SIGNATURE_VERIFICATION",
            "AiDailyExpense_DefaultSecureVaultEncryptionMasterKey_2026!",
            "DEV_SECRET_KEY_FOR_LOCAL_TESTING_ONLY_MINIMUM_32_CHARACTERS!",
            "YOUR_GEMINI_API_KEY",
            "YOUR_OPENAI_API_KEY",
            "YOUR_GMAIL_APP_PASSWORD",
            "YOUR_JWT_SECRET_KEY"
        };

        public static bool IsInsecureOrPlaceholder(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            var trimmed = key.Trim();
            if (trimmed.Length < 32)
            {
                return true;
            }

            if (KnownInsecurePlaceholders.Contains(trimmed))
            {
                return true;
            }

            if (trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("A_STRONG_", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("A_SECURE_", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("CHANGE_THIS_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static void ValidateProductionConfiguration(IConfiguration configuration, ILogger? logger = null)
        {
            var jwtKey = configuration["Jwt:Key"];
            if (IsInsecureOrPlaceholder(jwtKey))
            {
                const string msg = "FATAL: Jwt:Key chưa được cấu hình hoặc đang sử dụng placeholder công khai. Ứng dụng từ chối khởi động trong môi trường Production.";
                logger?.LogCritical("{Message}", msg);
                throw new InvalidOperationException(msg);
            }

            var vaultKey = configuration["Vault:MasterKey"];
            if (IsInsecureOrPlaceholder(vaultKey))
            {
                const string msg = "FATAL: Vault:MasterKey chưa được cấu hình hoặc đang sử dụng placeholder công khai. Ứng dụng từ chối khởi động trong môi trường Production.";
                logger?.LogCritical("{Message}", msg);
                throw new InvalidOperationException(msg);
            }

            // Verify that Vault:MasterKey is valid 32-byte Base64 key
            try
            {
                var keyBytes = Convert.FromBase64String(vaultKey!.Trim());
                if (keyBytes.Length != 32)
                {
                    const string msg = "FATAL: Vault:MasterKey phải là chuỗi Base64 đại diện cho đúng 32 bytes (256-bit AES key).";
                    logger?.LogCritical("{Message}", msg);
                    throw new InvalidOperationException(msg);
                }
            }
            catch (FormatException)
            {
                const string msg = "FATAL: Vault:MasterKey không phải định dạng Base64 hợp lệ.";
                logger?.LogCritical("{Message}", msg);
                throw new InvalidOperationException(msg);
            }
        }
    }
}
