using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services;
using AiDailyExpense.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();

// Swagger Configuration with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AI Daily Expense API", Version = "v1" });
    
    // Add JWT Authentication option to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

// Configure SQL Server Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var keyString = jwtSettings["Key"] ?? "CHANGE_THIS_SECRET_KEY_TO_A_LONG_RANDOM_VALUE_THAT_HAS_AT_LEAST_32_CHARACTERS_FOR_HMACSHA256_SIGNATURE_VERIFICATION";
var key = Encoding.UTF8.GetBytes(keyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "AiDailyExpense",
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"] ?? "AiDailyExpenseUsers",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Register Helpers
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JwtHelper>();
builder.Services.AddScoped<CurrentUserHelper>();

// Register Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IBankTransactionService, BankTransactionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IVaultService, VaultService>();
builder.Services.AddScoped<IStudyService, StudyService>();

var app = builder.Build();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
        await context.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('UserTasks', 'StartAt') IS NULL
    ALTER TABLE [UserTasks] ADD [StartAt] datetime2 NULL;
IF COL_LENGTH('UserTasks', 'EndAt') IS NULL
    ALTER TABLE [UserTasks] ADD [EndAt] datetime2 NULL;
IF COL_LENGTH('UserTasks', 'TaskType') IS NULL
    ALTER TABLE [UserTasks] ADD [TaskType] nvarchar(20) NOT NULL CONSTRAINT DF_UserTasks_TaskType DEFAULT(N'Task');
IF COL_LENGTH('UserTasks', 'Location') IS NULL
    ALTER TABLE [UserTasks] ADD [Location] nvarchar(200) NULL;
IF COL_LENGTH('Users', 'IsEmailVerified') IS NULL
    ALTER TABLE [Users] ADD [IsEmailVerified] bit NOT NULL CONSTRAINT DF_Users_IsEmailVerified DEFAULT(0);
IF COL_LENGTH('Users', 'EmailVerifiedAt') IS NULL
    ALTER TABLE [Users] ADD [EmailVerifiedAt] datetime2 NULL;
IF COL_LENGTH('Users', 'VaultPinHash') IS NULL
    ALTER TABLE [Users] ADD [VaultPinHash] nvarchar(500) NULL;
IF COL_LENGTH('Users', 'CashBalance') IS NULL
    ALTER TABLE [Users] ADD [CashBalance] decimal(18,2) NOT NULL CONSTRAINT DF_Users_CashBalance DEFAULT(0);
IF COL_LENGTH('Users', 'BankBalance') IS NULL
    ALTER TABLE [Users] ADD [BankBalance] decimal(18,2) NOT NULL CONSTRAINT DF_Users_BankBalance DEFAULT(0);
IF COL_LENGTH('Users', 'HasInitializedBalance') IS NULL
    ALTER TABLE [Users] ADD [HasInitializedBalance] bit NOT NULL CONSTRAINT DF_Users_HasInitializedBalance DEFAULT(0);
IF COL_LENGTH('Expenses', 'PaymentMethod') IS NULL
    ALTER TABLE [Expenses] ADD [PaymentMethod] nvarchar(20) NOT NULL CONSTRAINT DF_Expenses_PaymentMethod DEFAULT(N'Cash');
IF OBJECT_ID('EmailVerificationCodes', 'U') IS NULL
BEGIN
    CREATE TABLE [EmailVerificationCodes] (
        [EmailVerificationCodeId] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_EmailVerificationCodes] PRIMARY KEY,
        [UserId] uniqueidentifier NULL,
        [Email] nvarchar(255) NOT NULL,
        [Purpose] nvarchar(50) NOT NULL,
        [CodeHash] nvarchar(500) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConsumedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_EmailVerificationCodes_CreatedAt] DEFAULT(SYSDATETIME()),
        CONSTRAINT [FK_EmailVerificationCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users]([UserId]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_EmailCodes_Email_Purpose] ON [EmailVerificationCodes] ([Email], [Purpose], [ConsumedAt]);
END
IF OBJECT_ID('VaultCategories', 'U') IS NULL
BEGIN
    CREATE TABLE [VaultCategories] (
        [VaultCategoryId] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_VaultCategories] PRIMARY KEY,
        [UserId] uniqueidentifier NULL,
        [Name] nvarchar(100) NOT NULL,
        [Icon] nvarchar(50) NULL,
        [Color] nvarchar(20) NULL,
        [IsDefault] bit NOT NULL CONSTRAINT [DF_VaultCategories_IsDefault] DEFAULT(0),
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_VaultCategories_CreatedAt] DEFAULT(SYSDATETIME()),
        CONSTRAINT [FK_VaultCategories_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([UserId]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_VaultCategories_User_Name] ON [VaultCategories] ([UserId], [Name]);
END
IF OBJECT_ID('VaultItems', 'U') IS NULL
BEGIN
    CREATE TABLE [VaultItems] (
        [VaultItemId] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_VaultItems] PRIMARY KEY,
        [UserId] uniqueidentifier NOT NULL,
        [VaultCategoryId] int NULL,
        [Title] nvarchar(200) NOT NULL,
        [WebsiteUrl] nvarchar(500) NULL,
        [LoginUsername] nvarchar(255) NULL,
        [LoginEmail] nvarchar(255) NULL,
        [EncryptedPassword] varbinary(max) NOT NULL,
        [PasswordNonce] varbinary(32) NOT NULL,
        [PasswordAuthTag] varbinary(32) NULL,
        [EncryptedNotes] varbinary(max) NULL,
        [NotesNonce] varbinary(32) NULL,
        [NotesAuthTag] varbinary(32) NULL,
        [Favorite] bit NOT NULL CONSTRAINT [DF_VaultItems_Favorite] DEFAULT(0),
        [IsArchived] bit NOT NULL CONSTRAINT [DF_VaultItems_IsArchived] DEFAULT(0),
        [LastUsedAt] datetime2 NULL,
        [PasswordChangedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_VaultItems_CreatedAt] DEFAULT(SYSDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [FK_VaultItems_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([UserId]) ON DELETE CASCADE,
        CONSTRAINT [FK_VaultItems_VaultCategories] FOREIGN KEY ([VaultCategoryId]) REFERENCES [VaultCategories]([VaultCategoryId]) ON DELETE NO ACTION
    );
    CREATE INDEX [IX_VaultItems_UserId] ON [VaultItems] ([UserId]);
    CREATE INDEX [IX_VaultItems_User_Title] ON [VaultItems] ([UserId], [Title]);
END
IF NOT EXISTS (SELECT 1 FROM [VaultCategories] WHERE [UserId] IS NULL)
BEGIN
    INSERT INTO [VaultCategories] ([UserId], [Name], [Icon], [Color], [IsDefault])
    VALUES
    (NULL, N'Mạng xã hội', N'social', N'#3B82F6', 1),
    (NULL, N'Email', N'email', N'#10B981', 1),
    (NULL, N'Công việc', N'work', N'#6366F1', 1),
    (NULL, N'Ngân hàng', N'bank', N'#EF4444', 1),
    (NULL, N'Khác', N'other', N'#6B7280', 1);
END
IF OBJECT_ID('StudyDecks', 'U') IS NULL
BEGIN
    CREATE TABLE [StudyDecks] (
        [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_StudyDecks] PRIMARY KEY,
        [UserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Language] nvarchar(50) NOT NULL CONSTRAINT [DF_StudyDecks_Language] DEFAULT(N'other'),
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_StudyDecks_CreatedAt] DEFAULT(SYSDATETIME()),
        CONSTRAINT [FK_StudyDecks_Users] FOREIGN KEY ([UserId]) REFERENCES [Users]([UserId]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_StudyDecks_User_Name] ON [StudyDecks] ([UserId], [Name]);
END
IF OBJECT_ID('StudyDecks', 'U') IS NOT NULL AND COL_LENGTH('StudyDecks', 'Language') IS NULL
BEGIN
    ALTER TABLE [StudyDecks] ADD [Language] nvarchar(50) NOT NULL CONSTRAINT [DF_StudyDecks_Language] DEFAULT(N'other');
END
IF OBJECT_ID('StudyCards', 'U') IS NULL
BEGIN
    CREATE TABLE [StudyCards] (
        [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_StudyCards] PRIMARY KEY,
        [DeckId] int NOT NULL,
        [CardType] nvarchar(30) NOT NULL CONSTRAINT [DF_StudyCards_CardType] DEFAULT(N'Vocabulary'),
        [FrontText] nvarchar(1000) NOT NULL,
        [BackText] nvarchar(2000) NOT NULL,
        [Example] nvarchar(1000) NULL,
        [Note] nvarchar(1000) NULL,
        [Pronunciation] nvarchar(120) NULL,
        [Tags] nvarchar(300) NULL,
        [ExtendedData] nvarchar(max) NULL,
        [NormalizedText] nvarchar(300) NULL,
        [Difficulty] nvarchar(30) NOT NULL CONSTRAINT [DF_StudyCards_Difficulty] DEFAULT(N'New'),
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_StudyCards_CreatedAt] DEFAULT(SYSDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [FK_StudyCards_StudyDecks] FOREIGN KEY ([DeckId]) REFERENCES [StudyDecks]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_StudyCards_DeckId] ON [StudyCards] ([DeckId]);
    CREATE INDEX [IX_StudyCards_Deck_Type] ON [StudyCards] ([DeckId], [CardType]);
    CREATE INDEX [IX_StudyCards_Deck_NormalizedText] ON [StudyCards] ([DeckId], [NormalizedText]);
END
IF OBJECT_ID('StudyCards', 'U') IS NOT NULL AND COL_LENGTH('StudyCards', 'ExtendedData') IS NULL
BEGIN
    ALTER TABLE [StudyCards] ADD [ExtendedData] nvarchar(max) NULL;
END
IF OBJECT_ID('StudyCards', 'U') IS NOT NULL AND COL_LENGTH('StudyCards', 'NormalizedText') IS NULL
BEGIN
    ALTER TABLE [StudyCards] ADD [NormalizedText] nvarchar(300) NULL;
END
IF OBJECT_ID('StudyCards', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StudyCards_Deck_NormalizedText' AND object_id = OBJECT_ID('StudyCards'))
BEGIN
    CREATE INDEX [IX_StudyCards_Deck_NormalizedText] ON [StudyCards] ([DeckId], [NormalizedText]);
END
IF OBJECT_ID('StudyReviews', 'U') IS NULL
BEGIN
    CREATE TABLE [StudyReviews] (
        [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_StudyReviews] PRIMARY KEY,
        [CardId] bigint NOT NULL,
        [ReviewDate] datetime2 NOT NULL,
        [Rating] nvarchar(20) NOT NULL,
        [NextReviewDate] datetime2 NOT NULL,
        [IntervalDays] int NOT NULL,
        [EaseFactor] decimal(4,2) NOT NULL CONSTRAINT [DF_StudyReviews_EaseFactor] DEFAULT(2.5),
        [ReviewCount] int NOT NULL,
        CONSTRAINT [FK_StudyReviews_StudyCards] FOREIGN KEY ([CardId]) REFERENCES [StudyCards]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_StudyReviews_Card_NextReview] ON [StudyReviews] ([CardId], [NextReviewDate]);
    CREATE INDEX [IX_StudyReviews_ReviewDate] ON [StudyReviews] ([ReviewDate]);
END
");
        await DbSeeder.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Đã xảy ra lỗi khi khởi tạo hoặc seed dữ liệu database.");
    }
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AiDailyExpense.Api v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Redirect root to Swagger UI for convenient access
app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();

app.Run();
