using AuthService.Application.DTOs;
using AuthService.Application.Interfaces;
using AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AuthServiceImpl _authServiceImpl;

    private const string RefreshTokenCookieName = "refreshToken";

    private static CookieOptions RefreshCookieOptions => new CookieOptions
    {
        HttpOnly = true,           // Not accessible via JS (XSS protection)
        Secure = false,            // Set to true in production (HTTPS only)
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddDays(7)
    };


    public AuthController(IAuthService authService, AuthServiceImpl authServiceImpl)
    {
        _authService = authService;
        _authServiceImpl = authServiceImpl;
    }

    /// <summary>
    /// Registers a new passenger user in the database and returns an access token immediately.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        try
        {
            var accessToken = await _authService.RegisterAsync(dto);
            return Ok(new { accessToken });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }



    /// <summary>
    /// Authenticates a user.
    /// It returns an access JWT directly in the JSON response body to be held in JS memory,
    /// and securely sets the refresh token in an HttpOnly cookie to mitigate XSS attacks.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        try
        {
            var (response, refreshTokenValue) = await _authServiceImpl.LoginInternalAsync(dto);

            // Set refresh token in HttpOnly cookie (never sent to JS)
            Response.Cookies.Append(RefreshTokenCookieName, refreshTokenValue, RefreshCookieOptions);

            // Return only the access token + user info in the body
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Uses the HttpOnly refresh token cookie to issue a new access token.
    /// Implements refresh token rotation: old token is revoked, new one is issued.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { message = "No refresh token found" });

        try
        {
            var (response, newRefreshTokenValue) = await _authServiceImpl.RefreshTokenInternalAsync(refreshToken);

            // Rotate: set new refresh token in cookie
            Response.Cookies.Append(RefreshTokenCookieName, newRefreshTokenValue, RefreshCookieOptions);

            return Ok(response);
        }
        catch (Exception ex)
        {
            // Clear the invalid cookie
            Response.Cookies.Delete(RefreshTokenCookieName);
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Revokes the refresh token in DB and clears the cookie.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(refreshToken);
        }

        Response.Cookies.Delete(RefreshTokenCookieName);
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Allows a SuperAdmin to elevate a newly created user or register a new Administrator.
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("create-admin")]
    public async Task<IActionResult> CreateAdmin(RegisterDto dto)
    {
        try
        {
            await _authService.CreateAdminAsync(dto);
            return Ok(new { message = "Admin created successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Returns a list of all system users for administrative tracking.
    /// </summary>
    [Authorize(Roles = "SuperAdmin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _authService.GetAllUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            await _authService.DeleteUserAsync(id);
            return Ok(new { message = "User deleted" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Returns the currently authenticated user's profile details.
    /// Uses Claims extraction to get the UserId securely authenticated by the Gateway.
    /// </summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _authService.GetProfileAsync(userId);
        return Ok(user);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _authService.UpdateProfileAsync(userId, dto);
        return Ok(new { message = "Profile updated successfully" });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            await _authService.ChangePasswordAsync(userId, dto);
            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── Forgot / Reset Password (unauthenticated) ────────────────────────────

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        // Always 200 — never reveal whether the email exists
        await _authService.ForgotPasswordAsync(dto);
        return Ok(new { message = "If that email is registered, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto);
            return Ok(new { message = "Password reset successfully. You can now log in." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ─── Reward Points ───────────────────────────────────────────────

    /// <summary>Passenger views their reward points balance and history.</summary>
    [Authorize(Roles = "Passenger")]
    [HttpGet("rewards")]
    public async Task<IActionResult> GetRewards()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _authService.GetRewardPointsAsync(userId);
        return Ok(result);
    }

    /// <summary>Internal call from BookingService/PaymentService after payment confirmed.</summary>
    [AllowAnonymous]  // server-to-server trusted internal call
    [HttpPost("rewards/earn")]
    public async Task<IActionResult> EarnPoints(EarnPointsDto dto)
    {
        await _authService.EarnPointsAsync(dto);
        return Ok(new { message = "Points awarded" });
    }

    /// <summary>Passenger redeems points for a discount on the current booking.</summary>
    [Authorize(Roles = "Passenger")]
    [HttpPost("rewards/redeem")]
    public async Task<IActionResult> RedeemPoints(RedeemPointsDto dto)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var result = await _authService.RedeemPointsAsync(userId, dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Internal call from BookingService when a booking is cancelled — refunds redeemed points.</summary>
    [AllowAnonymous]  // server-to-server trusted internal call
    [HttpPost("rewards/refund")]
    public async Task<IActionResult> RefundPoints([FromQuery] int userId, [FromQuery] string referenceId)
    {
        await _authService.RefundPointsAsync(userId, referenceId);
        return Ok(new { message = "Points refunded" });
    }
}