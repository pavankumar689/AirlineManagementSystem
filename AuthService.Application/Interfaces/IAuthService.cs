using AuthService.Application.DTOs;

namespace AuthService.Application.Interfaces;

public interface IAuthService
{
    Task<string> RegisterAsync(RegisterDto dto);
    Task<LoginResponseDto> LoginAsync(LoginDto dto);
    Task<string> CreateAdminAsync(RegisterDto dto);
    Task<List<UserResponseDto>> GetAllUsersAsync();
    Task DeleteUserAsync(int id);
    Task<UserResponseDto> GetProfileAsync(int userId);
    Task UpdateProfileAsync(int userId, UpdateProfileDto dto);
    Task ChangePasswordAsync(int userId, ChangePasswordDto dto);

    // Password reset (unauthenticated)
    Task ForgotPasswordAsync(ForgotPasswordDto dto);
    Task ResetPasswordAsync(ResetPasswordDto dto);

    // Reward Points
    Task<RewardPointsDto> GetRewardPointsAsync(int userId);
    Task EarnPointsAsync(EarnPointsDto dto);
    Task<RedeemPointsResultDto> RedeemPointsAsync(int userId, RedeemPointsDto dto);
    Task RefundPointsAsync(int userId, string referenceId);

    // Refresh token operations
    Task<LoginResponseDto> RefreshTokenAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
}