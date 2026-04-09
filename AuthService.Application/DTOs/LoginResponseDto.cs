namespace AuthService.Application.DTOs;

/// <summary>
/// Returned to the client after login or token refresh.
/// accessToken is held in JS memory (not localStorage).
/// refreshToken is set in HttpOnly cookie by the controller — NOT in this DTO.
/// </summary>
public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
