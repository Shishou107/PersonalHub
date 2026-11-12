using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Models;

namespace AiDailyExpense.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!;
        public DbSet<UserTask> UserTasks { get; set; } = null!;
        public DbSet<Reminder> Reminders { get; set; } = null!;
        public DbSet<AiCommandLog> AiCommandLogs { get; set; } = null!;
        public DbSet<BankTransaction> BankTransactions { get; set; } = null!;
        public DbSet<DeviceToken> DeviceTokens { get; set; } = null!;
        public DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; } = null!;
        public DbSet<VaultCategory> VaultCategories { get; set; } = null!;
        public DbSet<VaultItem> VaultItems { get; set; } = null!;
        public DbSet<StudyDeck> StudyDecks { get; set; } = null!;
        public DbSet<StudyCard> StudyCards { get; set; } = null!;
        public DbSet<StudyReview> StudyReviews { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasDefaultValueSql("NEWID()");
                entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
                entity.Property(e => e.IsEmailVerified).HasDefaultValue(false);
                entity.Property(e => e.VaultPinHash).HasMaxLength(500);
                entity.Property(e => e.CashBalance).HasColumnType("decimal(18, 2)").HasDefaultValue(0);
                entity.Property(e => e.BankBalance).HasColumnType("decimal(18, 2)").HasDefaultValue(0);
                entity.Property(e => e.HasInitializedBalance).HasDefaultValue(false);
                entity.Property(e => e.TimeZone).HasMaxLength(100).HasDefaultValue("Asia/Ho_Chi_Minh");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");
            });

            // ExpenseCategory configuration
            modelBuilder.Entity<ExpenseCategory>(entity =>
            {
                entity.HasKey(e => e.CategoryId);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Type).HasMaxLength(20).HasDefaultValue("Expense");
                entity.Property(e => e.Icon).HasMaxLength(50);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.Property(e => e.IsDefault).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.ExpenseCategories)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Expense configuration
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.ExpenseId);
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
                entity.Property(e => e.PaymentMethod).HasMaxLength(20).HasDefaultValue("Cash");
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.RawText).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Expenses)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.Expenses)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.UserId, e.SpentAt }).HasDatabaseName("IX_Expenses_User_SpentAt");
                entity.HasIndex(e => new { e.UserId, e.CategoryId }).HasDatabaseName("IX_Expenses_User_Category");
            });

            // UserTask configuration
            modelBuilder.Entity<UserTask>(entity =>
            {
                entity.HasKey(e => e.TaskId);
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.StartAt);
                entity.Property(e => e.EndAt);
                entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
                entity.Property(e => e.Priority).HasMaxLength(20).HasDefaultValue("Normal");
                entity.Property(e => e.RepeatType).HasMaxLength(20).HasDefaultValue("None");
                entity.Property(e => e.TaskType).HasMaxLength(20).HasDefaultValue("Task");
                entity.Property(e => e.Location).HasMaxLength(200);
                entity.Property(e => e.RawText).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.UserTasks)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.DueAt }).HasDatabaseName("IX_UserTasks_User_DueAt");
                entity.HasIndex(e => new { e.UserId, e.Status }).HasDatabaseName("IX_UserTasks_User_Status");
            });

            // Reminder configuration
            modelBuilder.Entity<Reminder>(entity =>
            {
                entity.HasKey(e => e.ReminderId);
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Message).HasMaxLength(500);
                entity.Property(e => e.IsSent).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Reminders)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Task)
                    .WithMany(p => p.Reminders)
                    .HasForeignKey(d => d.TaskId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.RemindAt, e.IsSent }).HasDatabaseName("IX_Reminders_RemindAt_IsSent");
            });

            // AiCommandLog configuration
            modelBuilder.Entity<AiCommandLog>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.RawInput).HasMaxLength(2000).IsRequired();
                entity.Property(e => e.Intent).HasMaxLength(100);
                entity.Property(e => e.IsSuccess).HasDefaultValue(false);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.AiCommandLogs)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // BankTransaction configuration
            modelBuilder.Entity<BankTransaction>(entity =>
            {
                entity.HasKey(e => e.BankTransactionId);
                entity.Property(e => e.BankName).HasMaxLength(100);
                entity.Property(e => e.AccountLabel).HasMaxLength(100);
                entity.Property(e => e.TransactionType).HasMaxLength(20).HasDefaultValue("Unknown");
                entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
                entity.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("VND");
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Source).HasMaxLength(50).HasDefaultValue("Notification");
                entity.Property(e => e.RawMessage).HasMaxLength(1000);
                entity.Property(e => e.RawMessageHash).HasMaxLength(128);
                entity.Property(e => e.IsConfirmed).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.BankTransactions)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.BankTransactions)
                    .HasForeignKey(d => d.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => new { e.UserId, e.TransactionTime }).HasDatabaseName("IX_BankTransactions_User_Time");
                entity.HasIndex(e => new { e.UserId, e.TransactionType }).HasDatabaseName("IX_BankTransactions_User_Type");
                entity.HasIndex(e => new { e.UserId, e.RawMessageHash }).HasDatabaseName("IX_BankTransactions_User_RawHash");
            });

            // DeviceToken configuration
            modelBuilder.Entity<DeviceToken>(entity =>
            {
                entity.HasKey(e => e.DeviceTokenId);
                entity.Property(e => e.Token).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Platform).HasMaxLength(50).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.DeviceTokens)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.IsActive }).HasDatabaseName("IX_DeviceTokens_User_Active");
            });

            // EmailVerificationCode configuration
            modelBuilder.Entity<EmailVerificationCode>(entity =>
            {
                entity.HasKey(e => e.EmailVerificationCodeId);
                entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Purpose).HasMaxLength(50).IsRequired();
                entity.Property(e => e.CodeHash).HasMaxLength(500).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.EmailVerificationCodes)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.Email, e.Purpose, e.ConsumedAt }).HasDatabaseName("IX_EmailCodes_Email_Purpose");
            });

            modelBuilder.Entity<VaultCategory>(entity =>
            {
                entity.HasKey(e => e.VaultCategoryId);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Icon).HasMaxLength(50);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.VaultCategories)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.Name }).HasDatabaseName("IX_VaultCategories_User_Name");
            });

            modelBuilder.Entity<VaultItem>(entity =>
            {
                entity.HasKey(e => e.VaultItemId);
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.WebsiteUrl).HasMaxLength(500);
                entity.Property(e => e.LoginUsername).HasMaxLength(255);
                entity.Property(e => e.LoginEmail).HasMaxLength(255);
                entity.Property(e => e.EncryptedPassword).IsRequired();
                entity.Property(e => e.PasswordNonce).IsRequired();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.VaultItems)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.VaultCategory)
                    .WithMany(p => p.VaultItems)
                    .HasForeignKey(d => d.VaultCategoryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_VaultItems_UserId");
                entity.HasIndex(e => new { e.UserId, e.Title }).HasDatabaseName("IX_VaultItems_User_Title");
            });

            modelBuilder.Entity<StudyDeck>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Language).HasMaxLength(50).HasDefaultValue("other");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.StudyDecks)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.Name }).HasDatabaseName("IX_StudyDecks_User_Name");
            });

            modelBuilder.Entity<StudyCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CardType).HasMaxLength(30).HasDefaultValue("Vocabulary");
                entity.Property(e => e.FrontText).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.BackText).HasMaxLength(2000).IsRequired();
                entity.Property(e => e.Example).HasMaxLength(1000);
                entity.Property(e => e.Note).HasMaxLength(1000);
                entity.Property(e => e.Pronunciation).HasMaxLength(120);
                entity.Property(e => e.Tags).HasMaxLength(300);
                entity.Property(e => e.ExtendedData);
                entity.Property(e => e.NormalizedText).HasMaxLength(300);
                entity.Property(e => e.Difficulty).HasMaxLength(30).HasDefaultValue("New");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSDATETIME()");

                entity.HasOne(d => d.Deck)
                    .WithMany(p => p.StudyCards)
                    .HasForeignKey(d => d.DeckId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.DeckId).HasDatabaseName("IX_StudyCards_DeckId");
                entity.HasIndex(e => new { e.DeckId, e.CardType }).HasDatabaseName("IX_StudyCards_Deck_Type");
                entity.HasIndex(e => new { e.DeckId, e.NormalizedText }).HasDatabaseName("IX_StudyCards_Deck_NormalizedText");
            });

            modelBuilder.Entity<StudyReview>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Rating).HasMaxLength(20).IsRequired();
                entity.Property(e => e.EaseFactor).HasColumnType("decimal(4, 2)").HasDefaultValue(2.5m);

                entity.HasOne(d => d.Card)
                    .WithMany(p => p.StudyReviews)
                    .HasForeignKey(d => d.CardId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.CardId, e.NextReviewDate }).HasDatabaseName("IX_StudyReviews_Card_NextReview");
                entity.HasIndex(e => e.ReviewDate).HasDatabaseName("IX_StudyReviews_ReviewDate");
            });

            // Configure all DateTime properties to read/write as UTC
            var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            var nullableUtcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                v => v,
                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(utcConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(nullableUtcConverter);
                    }
                }
            }
        }
    }
}
