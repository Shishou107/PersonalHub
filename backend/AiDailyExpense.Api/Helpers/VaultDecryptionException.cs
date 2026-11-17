using System;

namespace AiDailyExpense.Api.Helpers
{
    public class VaultDecryptionException : Exception
    {
        public long VaultItemId { get; }

        public VaultDecryptionException()
        {
        }

        public VaultDecryptionException(string message)
            : base(message)
        {
        }

        public VaultDecryptionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public VaultDecryptionException(long vaultItemId, string message)
            : base(message)
        {
            VaultItemId = vaultItemId;
        }

        public VaultDecryptionException(long vaultItemId, string message, Exception innerException)
            : base(message, innerException)
        {
            VaultItemId = vaultItemId;
        }
    }
}
