using System.Text;
using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Data;
using AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using Scalar.AspNetCore;
using Shared.Events.ExceptionHandlers;

var logger = LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

logger.Info("AuthService starting up...");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Host.UseNLog();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthEmailService>();
// Register concrete type so AuthController can inject AuthServiceImpl directly
// (needed for the internal Login/Refresh helpers that return the refresh token value)
builder.Services.AddScoped<AuthServiceImpl>();
builder.Services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthServiceImpl>());

// Allow cookies to be sent cross-origin from Angular (localhost:4200 / 4201)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "http://localhost:4201")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();   // Required for cookies
    });
});

var jwtKey = builder.Configuration["Jwt:Key"]!;
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
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        // Accept tokens within 1 minute of expiry (clock skew buffer)
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AuthService API", Version = "v1" });

    c.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = JwtBearerDefaults.AuthenticationScheme
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
app.UseExceptionHandler();

// CORS must come before authentication
app.UseCors("AllowFrontend");

app.MapScalarApiReference(options =>
{
    options.WithTitle("AuthService API")
           .WithTheme(ScalarTheme.Purple)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
           .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});

app.UseSwagger();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Ensure Users table still exists (existing DB), and CREATE RefreshTokens if missing
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.EnsureCreated();

    // Create RefreshTokens table if it doesn't exist yet
    // Safe to run every startup — IF NOT EXISTS prevents duplication
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RefreshTokens' AND xtype='U')
        BEGIN
            CREATE TABLE RefreshTokens (
                Id          INT IDENTITY(1,1) PRIMARY KEY,
                Token       NVARCHAR(200)   NOT NULL UNIQUE,
                UserId      INT             NOT NULL,
                ExpiryDate  DATETIME2       NOT NULL,
                IsRevoked   BIT             NOT NULL DEFAULT 0,
                CreatedAt   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
                    REFERENCES Users(Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
        END
    """);

    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasswordResetTokens' AND xtype='U')
        BEGIN
            CREATE TABLE PasswordResetTokens (
                Id          INT IDENTITY(1,1) PRIMARY KEY,
                Token       NVARCHAR(500)   NOT NULL UNIQUE,
                UserId      INT             NOT NULL,
                ExpiryDate  DATETIME2       NOT NULL,
                IsUsed      BIT             NOT NULL DEFAULT 0,
                CreatedAt   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_PasswordResetTokens_Users FOREIGN KEY (UserId)
                    REFERENCES Users(Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IX_PasswordResetTokens_Token ON PasswordResetTokens(Token);
        END
    """);

    // Add RewardPoints column to Users if it doesn't exist yet
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (
            SELECT * FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'RewardPoints'
        )
        BEGIN
            ALTER TABLE Users ADD RewardPoints INT NOT NULL DEFAULT 0;
        END
    """);

    // Create RewardPointsLogs table if needed
    db.Database.ExecuteSqlRaw("""
        IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RewardPointsLogs' AND xtype='U')
        BEGIN
            CREATE TABLE RewardPointsLogs (
                Id          INT IDENTITY(1,1) PRIMARY KEY,
                UserId      INT             NOT NULL,
                Points      INT             NOT NULL,
                Type        NVARCHAR(50)    NOT NULL,
                Description NVARCHAR(500)   NOT NULL DEFAULT '',
                ReferenceId NVARCHAR(100)   NULL,
                CreatedAt   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT FK_RewardPointsLogs_Users FOREIGN KEY (UserId)
                    REFERENCES Users(Id) ON DELETE CASCADE
            );
        END
    """);
}

app.Run();