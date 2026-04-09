using AuthService.Application.DTOs;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Events.Exceptions;
using System.Security.Cryptography;

namespace AuthService.Infrastructure.Services;

public class AuthServiceImpl : IAuthService
{
    private readonly AuthDbContext _db;
    private readonly TokenService _tokenService;
    private readonly AuthEmailService _emailService;

    public AuthServiceImpl(AuthDbContext db, TokenService tokenService, AuthEmailService emailService)
    {
        _db = db;
        _tokenService = tokenService;
        _emailService = emailService;
    }

    public async Task<string> RegisterAsync(RegisterDto dto)
    {
        var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (exists)
            throw new ConflictException("User", "Email already registered");

        if (dto.Role == "Admin" || dto.Role == "SuperAdmin")
            dto.Role = "Passenger";

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Registration returns only access token (no cookie needed — user must explicitly log in)
        return _tokenService.GenerateAccessToken(user);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            throw new AuthException("Invalid email or password");

        var valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!valid)
            throw new AuthException("Invalid email or password");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email,
            // RefreshToken value is needed by the controller to set the cookie
            // We pass it via a transient property pattern below
        };
    }

    /// <summary>
    /// Internal helper — returns both the LoginResponseDto AND the raw refresh token string
    /// so the controller can set the HttpOnly cookie.
    /// </summary>
    public async Task<(LoginResponseDto response, string refreshTokenValue)> LoginInternalAsync(LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            throw new AuthException("Invalid email or password");

        var valid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!valid)
            throw new AuthException("Invalid email or password");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        var response = new LoginResponseDto
        {
            AccessToken = accessToken,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email
        };

        return (response, refreshToken.Token);
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (stored == null || stored.IsRevoked || stored.ExpiryDate < DateTime.UtcNow)
            throw new AuthException("Invalid or expired refresh token");

        // Revoke the old refresh token (rotation)
        stored.IsRevoked = true;

        // Issue new access token and new refresh token
        var newAccessToken = _tokenService.GenerateAccessToken(stored.User);
        var newRefreshToken = _tokenService.GenerateRefreshToken(stored.UserId);

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync();

        return new LoginResponseDto
        {
            AccessToken = newAccessToken,
            Role = stored.User.Role,
            FullName = stored.User.FullName,
            Email = stored.User.Email
        };
    }

    // Private helper to get the new refresh token value after rotation
    public async Task<(LoginResponseDto response, string newRefreshTokenValue)> RefreshTokenInternalAsync(string refreshToken)
    {
        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (stored == null || stored.IsRevoked || stored.ExpiryDate < DateTime.UtcNow)
            throw new AuthException("Invalid or expired refresh token");

        stored.IsRevoked = true;

        var newAccessToken = _tokenService.GenerateAccessToken(stored.User);
        var newRefreshToken = _tokenService.GenerateRefreshToken(stored.UserId);

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync();

        var response = new LoginResponseDto
        {
            AccessToken = newAccessToken,
            Role = stored.User.Role,
            FullName = stored.User.FullName,
            Email = stored.User.Email
        };

        return (response, newRefreshToken.Token);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (stored != null)
        {
            stored.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<UserResponseDto>> GetAllUsersAsync()
    {
        var users = await _db.Users.ToListAsync();
        return users.Select(u => new UserResponseDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = u.Role,
            CreatedAt = u.CreatedAt
        }).ToList();
    }

    public async Task DeleteUserAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            throw new NotFoundException("User", id);

        if (user.Role == "SuperAdmin")
            throw new ConflictException("User", "Cannot delete SuperAdmin");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    public async Task<string> CreateAdminAsync(RegisterDto dto)
    {
        var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (exists)
            throw new ConflictException("User", "Email already registered");

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return _tokenService.GenerateAccessToken(user);
    }

    public async Task<UserResponseDto> GetProfileAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);
        return new UserResponseDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task UpdateProfileAsync(int userId, UpdateProfileDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);
        user.FullName = dto.FullName;
        user.Email = dto.Email;
        await _db.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);

        var valid = BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash);
        if (!valid) throw new AuthException("Current password is incorrect");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _db.SaveChangesAsync();
    }

    // ─── Forgot / Reset Password ───────────────────────────────────────────────

    public async Task ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        // Always return success to client (don't reveal if email exists)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null) return;

        // Invalidate any existing unused tokens for this user
        var oldTokens = _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed);
        _db.PasswordResetTokens.RemoveRange(oldTokens);

        // Generate a secure raw token (URL-safe base64)
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        // Store hashed version in DB
        var hashedToken = BCrypt.Net.BCrypt.HashPassword(rawToken);

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            Token = hashedToken,
            ExpiryDate = DateTime.UtcNow.AddHours(1),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync();

        // Build reset link — carries userId + rawToken as query params
        var resetLink = $"http://localhost:4201/reset-password?userId={user.Id}&token={Uri.EscapeDataString(rawToken)}";

        await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, resetLink);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        // Find all non-expired, unused tokens for this user
        var tokens = await _db.PasswordResetTokens
            .Where(t => t.UserId == dto.UserId && !t.IsUsed && t.ExpiryDate > DateTime.UtcNow)
            .ToListAsync();

        // Find the one matching our raw token
        var matched = tokens.FirstOrDefault(t => BCrypt.Net.BCrypt.Verify(dto.Token, t.Token));
        if (matched == null)
            throw new AuthException("Invalid or expired reset link. Please request a new one.");

        var user = await _db.Users.FindAsync(dto.UserId);
        if (user == null) throw new NotFoundException("User", dto.UserId);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        matched.IsUsed = true;

        await _db.SaveChangesAsync();
    }

    // ─── Reward Points ─────────────────────────────────────────────────────

    // 10 points earned per ₹100 spent (i.e. 10% earn rate)
    private static int CalculateEarnedPoints(decimal amount) =>
        (int)(amount / 10);  // 1 point per ₹10

    public async Task<RewardPointsDto> GetRewardPointsAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);

        var logs = await _db.RewardPointsLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(50)
            .ToListAsync();

        return new RewardPointsDto
        {
            Balance = user.RewardPoints,
            History = logs.Select(l => new RewardPointsLogDto
            {
                Points = l.Points,
                Type = l.Type,
                Description = l.Description,
                ReferenceId = l.ReferenceId,
                CreatedAt = l.CreatedAt
            }).ToList()
        };
    }

    public async Task EarnPointsAsync(EarnPointsDto dto)
    {
        var user = await _db.Users.FindAsync(dto.UserId);
        if (user == null) return;

        var pointsEarned = CalculateEarnedPoints(dto.AmountPaid);
        if (pointsEarned <= 0) return;

        user.RewardPoints += pointsEarned;

        _db.RewardPointsLogs.Add(new RewardPointsLog
        {
            UserId = dto.UserId,
            Points = pointsEarned,
            Type = "Earned",
            Description = $"Earned {pointsEarned} pts for booking payment of ₹{dto.AmountPaid:N0}",
            ReferenceId = dto.ReferenceId
        });

        await _db.SaveChangesAsync();
    }

    public async Task<RedeemPointsResultDto> RedeemPointsAsync(int userId, RedeemPointsDto dto)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new NotFoundException("User", userId);

        if (dto.PointsToRedeem <= 0)
            throw new ValidationException("PointsToRedeem", "Points to redeem must be greater than zero");

        if (user.RewardPoints < dto.PointsToRedeem)
            throw new ValidationException("PointsToRedeem", $"Insufficient points. You have {user.RewardPoints} pts.");

        // Cap: max 60% of the booking total can be discounted via points
        var maxDiscountAllowed = dto.BookingTotal * 0.60m;
        var requestedDiscount = (decimal)dto.PointsToRedeem;  // 1 pt = ₹1

        var actualDiscount = Math.Min(requestedDiscount, maxDiscountAllowed);
        var actualPoints = (int)Math.Floor(actualDiscount); // points used = discount given (1:1)

        user.RewardPoints -= actualPoints;

        _db.RewardPointsLogs.Add(new RewardPointsLog
        {
            UserId = userId,
            Points = -actualPoints,
            Type = "Redeemed",
            Description = $"Redeemed {actualPoints} pts for ₹{actualDiscount:N0} discount on booking",
            ReferenceId = dto.ReferenceId
        });

        await _db.SaveChangesAsync();

        return new RedeemPointsResultDto
        {
            PointsUsed = actualPoints,
            DiscountAmount = actualDiscount,
            RemainingBalance = user.RewardPoints
        };
    }

    public async Task RefundPointsAsync(int userId, string referenceId)
    {
        // Find the redemption log entry for this reference
        var redemption = await _db.RewardPointsLogs
            .FirstOrDefaultAsync(l =>
                l.UserId == userId &&
                l.ReferenceId == referenceId &&
                l.Type == "Redeemed");

        if (redemption == null) return; // No points were redeemed for this booking

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        var refundPoints = Math.Abs(redemption.Points);
        user.RewardPoints += refundPoints;

        _db.RewardPointsLogs.Add(new RewardPointsLog
        {
            UserId = userId,
            Points = refundPoints,
            Type = "Refunded",
            Description = $"Refunded {refundPoints} pts for cancelled booking",
            ReferenceId = referenceId
        });

        await _db.SaveChangesAsync();
    }
}