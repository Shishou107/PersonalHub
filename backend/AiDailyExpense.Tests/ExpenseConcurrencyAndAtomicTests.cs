using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Auth;
using AiDailyExpense.Api.DTOs.BankTransactions;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services;
using AiDailyExpense.Api.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiDailyExpense.Tests
{
    public class ExpenseConcurrencyAndAtomicTests : IDisposable
    {
        private const string ConnectionString = "Data Source=ExpenseConcurrencyDb;Mode=Memory;Cache=Shared;Default Timeout=30";
        private readonly SqliteConnection _keepAliveConnection;

        public ExpenseConcurrencyAndAtomicTests()
        {
            // Keep master connection open so the shared in-memory database is not destroyed
            _keepAliveConnection = new SqliteConnection(ConnectionString);
            _keepAliveConnection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_keepAliveConnection)
                .Options;

            using var context = new AppDbContext(options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _keepAliveConnection.Dispose();
            GC.SuppressFinalize(this);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreateExpenseAsync_UpdatesBalanceAtomically_AndPreservesTotals()
        {
            var userId = Guid.NewGuid();
            using (var seedContext = CreateContext())
            {
                var user = new User
                {
                    UserId = userId,
                    FullName = "Atomic User",
                    Email = "atomic@example.com",
                    CashBalance = 1_000_000m,
                    BankBalance = 2_000_000m,
                    HasInitializedBalance = true,
                    CreatedAt = DateTime.UtcNow
                };
                var categoryExpense = new ExpenseCategory
                {
                    CategoryId = 1,
                    UserId = userId,
                    Name = "Ăn uống",
                    Type = "Expense",
                    CreatedAt = DateTime.UtcNow
                };
                var categoryIncome = new ExpenseCategory
                {
                    CategoryId = 2,
                    UserId = userId,
                    Name = "Lương",
                    Type = "Income",
                    CreatedAt = DateTime.UtcNow
                };
                seedContext.Users.Add(user);
                seedContext.ExpenseCategories.AddRange(categoryExpense, categoryIncome);
                await seedContext.SaveChangesAsync();
            }

            // 1. Spend 100,000 cash on Expense (Cash should decrease by 100,000)
            using (var context1 = CreateContext())
            {
                var service = new ExpenseService(context1);
                await service.CreateExpenseAsync(userId, new CreateExpenseRequest
                {
                    Title = "Ăn trưa",
                    Amount = 100_000m,
                    CategoryId = 1,
                    PaymentMethod = "Cash",
                    SpentAt = DateTime.UtcNow
                });
            }

            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.NotNull(user);
                Assert.Equal(900_000m, user.CashBalance);
                Assert.Equal(2_000_000m, user.BankBalance);
            }

            // 2. Spend 500,000 transfer on Expense (Bank should decrease by 500,000)
            using (var context2 = CreateContext())
            {
                var service = new ExpenseService(context2);
                await service.CreateExpenseAsync(userId, new CreateExpenseRequest
                {
                    Title = "Mua sắm online",
                    Amount = 500_000m,
                    CategoryId = 1,
                    PaymentMethod = "Transfer",
                    SpentAt = DateTime.UtcNow
                });
            }

            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.NotNull(user);
                Assert.Equal(900_000m, user.CashBalance);
                Assert.Equal(1_500_000m, user.BankBalance);
            }

            // 3. Receive 2,000,000 transfer as Income (Bank should increase by 2,000,000)
            using (var context3 = CreateContext())
            {
                var service = new ExpenseService(context3);
                await service.CreateExpenseAsync(userId, new CreateExpenseRequest
                {
                    Title = "Nhận lương",
                    Amount = 2_000_000m,
                    CategoryId = 2,
                    PaymentMethod = "Transfer",
                    SpentAt = DateTime.UtcNow
                });
            }

            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.NotNull(user);
                Assert.Equal(900_000m, user.CashBalance);
                Assert.Equal(3_500_000m, user.BankBalance);
            }
        }

        [Fact]
        public async Task ExpenseService_Rollback_OnInvalidOperation_LeavesBalanceAndExpensesUnchanged()
        {
            var userId = Guid.NewGuid();
            using (var seedContext = CreateContext())
            {
                var user = new User
                {
                    UserId = userId,
                    FullName = "Rollback User",
                    Email = "rollback@example.com",
                    CashBalance = 500_000m,
                    BankBalance = 1_000_000m,
                    HasInitializedBalance = true,
                    CreatedAt = DateTime.UtcNow
                };
                seedContext.Users.Add(user);
                await seedContext.SaveChangesAsync();
            }

            using (var context = CreateContext())
            {
                var service = new ExpenseService(context);

                // Attempt to create expense with non-existent category -> throws ArgumentException before commit
                await Assert.ThrowsAsync<ArgumentException>(() =>
                    service.CreateExpenseAsync(userId, new CreateExpenseRequest
                    {
                        Title = "Invalid Category Expense",
                        Amount = 100_000m,
                        CategoryId = 99999, // non-existent
                        PaymentMethod = "Cash",
                        SpentAt = DateTime.UtcNow
                    }));
            }

            // Verify that no expense was saved and balances remain unchanged
            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.NotNull(user);
                Assert.Equal(500_000m, user.CashBalance);
                Assert.Equal(1_000_000m, user.BankBalance);

                var expenseCount = await verifyContext.Expenses.CountAsync(e => e.UserId == userId);
                Assert.Equal(0, expenseCount);
            }
        }

        [Fact]
        public async Task UpdateExpenseAsync_AdjustsBalanceAtomically()
        {
            var userId = Guid.NewGuid();
            long expenseId;

            using (var seedContext = CreateContext())
            {
                var user = new User
                {
                    UserId = userId,
                    FullName = "Update User",
                    Email = "update@example.com",
                    CashBalance = 500_000m,
                    BankBalance = 500_000m,
                    HasInitializedBalance = true,
                    CreatedAt = DateTime.UtcNow
                };
                var category = new ExpenseCategory
                {
                    CategoryId = 1,
                    UserId = userId,
                    Name = "Mua sắm",
                    Type = "Expense",
                    CreatedAt = DateTime.UtcNow
                };
                seedContext.Users.Add(user);
                seedContext.ExpenseCategories.Add(category);
                await seedContext.SaveChangesAsync();

                var service = new ExpenseService(seedContext);
                var created = await service.CreateExpenseAsync(userId, new CreateExpenseRequest
                {
                    Title = "Mua giày",
                    Amount = 100_000m,
                    CategoryId = 1,
                    PaymentMethod = "Cash",
                    SpentAt = DateTime.UtcNow
                });
                expenseId = created.ExpenseId;
            }

            // Verify initial balance after creation: 500k - 100k = 400k
            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.Equal(400_000m, user!.CashBalance);
            }

            // Update amount from 100,000 Cash to 150,000 Cash -> Balance should become 350,000
            using (var updateContext = CreateContext())
            {
                var service = new ExpenseService(updateContext);
                await service.UpdateExpenseAsync(userId, expenseId, new UpdateExpenseRequest
                {
                    Title = "Mua giày xịn",
                    Amount = 150_000m,
                    CategoryId = 1,
                    PaymentMethod = "Cash",
                    SpentAt = DateTime.UtcNow
                });
            }

            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.Equal(350_000m, user!.CashBalance);
            }

            // Update payment method from Cash to Transfer -> Cash restored to 500k, Bank becomes 500k - 150k = 350k
            using (var updateContext = CreateContext())
            {
                var service = new ExpenseService(updateContext);
                await service.UpdateExpenseAsync(userId, expenseId, new UpdateExpenseRequest
                {
                    Title = "Mua giày xịn",
                    Amount = 150_000m,
                    CategoryId = 1,
                    PaymentMethod = "Transfer",
                    SpentAt = DateTime.UtcNow
                });
            }

            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.Equal(500_000m, user!.CashBalance);
                Assert.Equal(350_000m, user!.BankBalance);
            }
        }

        [Fact]
        public async Task DeleteExpenseAsync_RestoresBalanceAtomically()
        {
            var userId = Guid.NewGuid();
            long expenseId;

            using (var seedContext = CreateContext())
            {
                var user = new User
                {
                    UserId = userId,
                    FullName = "Delete User",
                    Email = "delete@example.com",
                    CashBalance = 1_000_000m,
                    BankBalance = 1_000_000m,
                    HasInitializedBalance = true,
                    CreatedAt = DateTime.UtcNow
                };
                var category = new ExpenseCategory
                {
                    CategoryId = 1,
                    UserId = userId,
                    Name = "Ăn uống",
                    Type = "Expense",
                    CreatedAt = DateTime.UtcNow
                };
                seedContext.Users.Add(user);
                seedContext.ExpenseCategories.Add(category);
                await seedContext.SaveChangesAsync();

                var service = new ExpenseService(seedContext);
                var created = await service.CreateExpenseAsync(userId, new CreateExpenseRequest
                {
                    Title = "Tiệc sinh nhật",
                    Amount = 250_000m,
                    CategoryId = 1,
                    PaymentMethod = "Bank", // normalized to Cash/Transfer
                    SpentAt = DateTime.UtcNow
                });
                expenseId = created.ExpenseId;
            }

            using (var deleteContext = CreateContext())
            {
                var service = new ExpenseService(deleteContext);
                var deleted = await service.DeleteExpenseAsync(userId, expenseId);
                Assert.True(deleted);
            }

            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.Equal(1_000_000m, user!.CashBalance);
                Assert.Equal(1_000_000m, user!.BankBalance);

                var count = await verifyContext.Expenses.CountAsync(e => e.UserId == userId);
                Assert.Equal(0, count);
            }
        }

        [Fact]
        public async Task Concurrent_BalanceUpdates_ExecuteAtomicallyWithoutLostUpdates()
        {
            var userId = Guid.NewGuid();
            using (var seedContext = CreateContext())
            {
                var user = new User
                {
                    UserId = userId,
                    FullName = "Concurrent User",
                    Email = "concurrent@example.com",
                    CashBalance = 1_000_000m,
                    BankBalance = 1_000_000m,
                    HasInitializedBalance = true,
                    CreatedAt = DateTime.UtcNow
                };
                var category = new ExpenseCategory
                {
                    CategoryId = 1,
                    UserId = userId,
                    Name = "Chi tiêu",
                    Type = "Expense",
                    CreatedAt = DateTime.UtcNow
                };
                seedContext.Users.Add(user);
                seedContext.ExpenseCategories.Add(category);
                await seedContext.SaveChangesAsync();
            }

            // Simulate 6 concurrent transactions impacting the user's balances:
            // 3 Cash expenses of 50,000 each (Total: 150,000 cash deducted)
            // 3 Transfer expenses of 100,000 each (Total: 300,000 bank deducted)
            var tasks = new List<Task>();

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    using var ctx = CreateContext();
                    var service = new ExpenseService(ctx);
                    await service.CreateExpenseAsync(userId, new CreateExpenseRequest
                    {
                        Title = $"Cash Expense {index}",
                        Amount = 50_000m,
                        CategoryId = 1,
                        PaymentMethod = "Cash",
                        SpentAt = DateTime.UtcNow
                    });
                }));
            }

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    using var ctx = CreateContext();
                    var service = new ExpenseService(ctx);
                    await service.CreateExpenseAsync(userId, new CreateExpenseRequest
                    {
                        Title = $"Transfer Expense {index}",
                        Amount = 100_000m,
                        CategoryId = 1,
                        PaymentMethod = "Transfer",
                        SpentAt = DateTime.UtcNow
                    });
                }));
            }

            await Task.WhenAll(tasks);

            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.NotNull(user);
                // Cash: 1,000,000 - 150,000 = 850,000
                Assert.Equal(850_000m, user.CashBalance);
                // Bank: 1,000,000 - 300,000 = 700,000
                Assert.Equal(700_000m, user.BankBalance);

                var totalExpenses = await verifyContext.Expenses.CountAsync(e => e.UserId == userId);
                Assert.Equal(6, totalExpenses);
            }
        }

        [Fact]
        public async Task AuthService_InitializeBalanceAsync_SetsBalancesAtomically_AndPreventsDoubleInitialization()
        {
            var userId = Guid.NewGuid();
            using (var seedContext = CreateContext())
            {
                var user = new User
                {
                    UserId = userId,
                    FullName = "Init User",
                    Email = "init@example.com",
                    CashBalance = 0,
                    BankBalance = 0,
                    HasInitializedBalance = false,
                    CreatedAt = DateTime.UtcNow
                };
                seedContext.Users.Add(user);
                await seedContext.SaveChangesAsync();
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", "A_VERY_LONG_RANDOM_SECRET_KEY_FOR_TESTING_PURPOSES_ONLY_MIN_32_CHARACTERS" }
                })
                .Build();
            var jwtHelper = new JwtHelper(config);
            var dummyEmail = new DummyEmailService();

            // First initialization succeeds
            using (var initContext = CreateContext())
            {
                var authService = new AuthService(initContext, jwtHelper, dummyEmail);
                var result = await authService.InitializeBalanceAsync(userId, new InitializeBalanceRequest
                {
                    CashBalance = 2_500_000m,
                    BankBalance = 8_000_000m
                });

                Assert.Equal(2_500_000m, result.CashBalance);
                Assert.Equal(8_000_000m, result.BankBalance);
                Assert.True(result.HasInitializedBalance);
            }

            // Second initialization fails fast with InvalidOperationException
            using (var duplicateContext = CreateContext())
            {
                var authService = new AuthService(duplicateContext, jwtHelper, dummyEmail);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    authService.InitializeBalanceAsync(userId, new InitializeBalanceRequest
                    {
                        CashBalance = 9_999_999m,
                        BankBalance = 9_999_999m
                    }));
            }

            // Verify database was not overwritten
            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.NotNull(user);
                Assert.Equal(2_500_000m, user.CashBalance);
                Assert.Equal(8_000_000m, user.BankBalance);
            }
        }

        [Fact]
        public async Task BankTransactionService_ConfirmTransactionAsync_ExecutesAtomically()
        {
            var userId = Guid.NewGuid();
            long bankTxId;

            using (var seedContext = CreateContext())
            {
                var user = new User
                {
                    UserId = userId,
                    FullName = "Bank User",
                    Email = "bank@example.com",
                    CashBalance = 0,
                    BankBalance = 5_000_000m,
                    HasInitializedBalance = true,
                    CreatedAt = DateTime.UtcNow
                };
                var category = new ExpenseCategory
                {
                    CategoryId = 1,
                    UserId = userId,
                    Name = "Mua sắm",
                    Type = "Expense",
                    CreatedAt = DateTime.UtcNow
                };
                var bankTx = new BankTransaction
                {
                    UserId = userId,
                    BankName = "Vietcombank",
                    Amount = 500_000m,
                    Description = "Chuyen khoan Shopee",
                    IsConfirmed = false,
                    TransactionTime = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                seedContext.Users.Add(user);
                seedContext.ExpenseCategories.Add(category);
                seedContext.BankTransactions.Add(bankTx);
                await seedContext.SaveChangesAsync();
                bankTxId = bankTx.BankTransactionId;
            }

            using (var confirmContext = CreateContext())
            {
                var expenseService = new ExpenseService(confirmContext);
                var bankService = new BankTransactionService(confirmContext, expenseService, null!);
                var expense = await bankService.ConfirmTransactionAsync(userId, bankTxId, new ConfirmBankTransactionRequest
                {
                    CategoryId = 1
                });

                Assert.NotNull(expense);
                Assert.Equal(500_000m, expense.Amount);
            }

            // Verify: BankBalance decreased by 500,000, BankTransaction is marked IsConfirmed = true, and Expense is created
            using (var verifyContext = CreateContext())
            {
                var user = await verifyContext.Users.FindAsync(userId);
                Assert.NotNull(user);
                Assert.Equal(4_500_000m, user.BankBalance);

                var tx = await verifyContext.BankTransactions.FindAsync(bankTxId);
                Assert.NotNull(tx);
                Assert.True(tx.IsConfirmed);

                var expenses = await verifyContext.Expenses.Where(e => e.UserId == userId).ToListAsync();
                Assert.Single(expenses);
                Assert.Equal(500_000m, expenses[0].Amount);
                Assert.Equal("Transfer", expenses[0].PaymentMethod);
            }
        }
    }
}
