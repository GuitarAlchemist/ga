namespace GaApi.Controllers;

using System.Security.Claims;
using AspNet.Security.OAuth.GitHub;
using GaApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

/// <summary>
/// OAuth2 login + JWT issuance.
///
/// Flow:
/// 1. Client opens GET /api/auth/challenge/{provider}?returnUrl=https://...
///    → 302 to the provider's OAuth consent page
/// 2. Provider redirects back to GET /api/auth/callback/{provider}
///    → we read the external claims, upsert the user, issue JWT + refresh
///    → 302 to {returnUrl}#access_token={jwt}&expires_in={seconds}
/// 3. Client extracts the JWT from the URL fragment, stores in sessionStorage,
///    keeps the httpOnly refresh cookie automatically.
/// 4. On 401, client POSTs /api/auth/refresh → new JWT.
/// 5. Client POSTs /api/auth/logout to revoke the refresh cookie.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "ga_refresh";
    private static readonly TimeSpan RefreshCookieTtl = TimeSpan.FromDays(30);

    private readonly UserService _userService;
    private readonly TokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserService userService,
        TokenService tokenService,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    /// <summary>Kick off the OAuth flow with the selected provider.</summary>
    [HttpGet("challenge/{provider}")]
    public async Task<IActionResult> Challenge(
        [FromRoute] string provider,
        [FromQuery] string? returnUrl,
        [FromServices] IAuthenticationSchemeProvider schemeProvider)
    {
        var scheme = ResolveScheme(provider);
        if (scheme is null) return BadRequest(new { error = "unsupported provider" });

        // If the provider's handler isn't registered, it means the client ID/Secret
        // aren't configured (Program.cs skips AddGoogle()/AddGitHub() when missing).
        // Return a clean error instead of letting the framework throw a 500.
        var registered = await schemeProvider.GetSchemeAsync(scheme);
        if (registered is null)
        {
            _logger.LogWarning("OAuth provider {Provider} challenged but handler not registered — missing ClientId/Secret?", provider);
            return BadRequest(new
            {
                error = "provider_not_configured",
                detail = $"The {provider} OAuth handler is not registered. Set Authentication:{char.ToUpperInvariant(provider[0])}{provider[1..]}:ClientId and :ClientSecret via dotnet user-secrets.",
            });
        }

        // The callback endpoint reconstructs returnUrl from this item key
        var props = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback), new { provider }) ?? $"/api/auth/callback/{provider}",
            Items = { ["returnUrl"] = returnUrl ?? "/" },
        };
        return Challenge(props, scheme);
    }

    /// <summary>OAuth callback — upsert user, issue JWT + refresh cookie, 302 to app.</summary>
    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback([FromRoute] string provider, CancellationToken ct)
    {
        var scheme = ResolveScheme(provider);
        if (scheme is null) return BadRequest(new { error = "unsupported provider" });

        var result = await HttpContext.AuthenticateAsync(scheme);
        if (!result.Succeeded || result.Principal is null)
        {
            _logger.LogWarning("OAuth {Provider} callback failed: {Failure}", provider, result.Failure?.Message);
            return Redirect($"/?auth_error={Uri.EscapeDataString("external_auth_failed")}");
        }

        var claims = result.Principal;
        var providerUserId =
            claims.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? claims.FindFirstValue("sub")
            ?? claims.FindFirstValue("id");
        var email = claims.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var name =
            claims.FindFirstValue(ClaimTypes.Name)
            ?? claims.FindFirstValue("name")
            ?? email;
        var avatar =
            claims.FindFirstValue("urn:github:avatar")
            ?? claims.FindFirstValue("picture")
            ?? claims.FindFirstValue("avatar_url");

        if (string.IsNullOrEmpty(providerUserId))
        {
            _logger.LogWarning("OAuth {Provider} returned no stable user id", provider);
            return Redirect($"/?auth_error={Uri.EscapeDataString("no_provider_id")}");
        }

        var user = await _userService.FindOrCreateAsync(provider, providerUserId, email, name, avatar, ct);

        // Sign out the external scheme's cookie — we use JWT for API auth going forward
        await HttpContext.SignOutAsync(scheme);

        var jwt = _tokenService.IssueJwt(user);
        var clientInfo = HttpContext.Connection.RemoteIpAddress?.ToString();
        var refreshRaw = await _tokenService.IssueRefreshTokenAsync(user.Id, clientInfo, ct);

        SetRefreshCookie(refreshRaw);

        string? returnUrl = null;
        result.Properties?.Items.TryGetValue("returnUrl", out returnUrl);
        var target = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        var expiresIn = _jwtSettings.AccessTokenMinutes * 60;
        var fragment = $"access_token={Uri.EscapeDataString(jwt)}&expires_in={expiresIn}&token_type=Bearer";
        return Redirect($"{target}#{fragment}");
    }

    /// <summary>Exchange the refresh cookie for a new JWT.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var raw) || string.IsNullOrEmpty(raw))
            return Unauthorized(new { error = "no_refresh_cookie" });

        var userId = await _tokenService.ValidateRefreshAsync(raw, ct);
        if (userId is null) return Unauthorized(new { error = "invalid_or_expired" });

        // Rotate: revoke the old token, issue a new one
        await _tokenService.RevokeRefreshAsync(raw, ct);
        var user = await _userService.GetByIdAsync(userId.Value, ct);
        if (user is null) return Unauthorized(new { error = "user_not_found" });

        var clientInfo = HttpContext.Connection.RemoteIpAddress?.ToString();
        var newRaw = await _tokenService.IssueRefreshTokenAsync(user.Id, clientInfo, ct);
        SetRefreshCookie(newRaw);

        var jwt = _tokenService.IssueJwt(user);
        return Ok(new
        {
            access_token = jwt,
            expires_in = _jwtSettings.AccessTokenMinutes * 60,
            token_type = "Bearer",
        });
    }

    /// <summary>Revoke the refresh cookie and clear it from the browser.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var raw) && !string.IsNullOrEmpty(raw))
            await _tokenService.RevokeRefreshAsync(raw, ct);
        Response.Cookies.Delete(RefreshCookieName);
        return Ok(new { ok = true });
    }

    /// <summary>Return the current user from the Bearer JWT.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !ObjectId.TryParse(sub, out var userId))
            return Unauthorized();
        var user = await _userService.GetByIdAsync(userId, ct);
        if (user is null) return Unauthorized();
        return Ok(new
        {
            id = user.Id.ToString(),
            email = user.Email,
            name = user.Name,
            avatarUrl = user.AvatarUrl,
            provider = user.Provider,
            roles = user.Roles,
        });
    }

    private static string? ResolveScheme(string provider) => provider?.ToLowerInvariant() switch
    {
        "google" => GoogleDefaults.AuthenticationScheme,
        "github" => GitHubAuthenticationDefaults.AuthenticationScheme,
        _ => null,
    };

    private void SetRefreshCookie(string raw)
    {
        Response.Cookies.Append(RefreshCookieName, raw, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.Add(RefreshCookieTtl),
        });
    }
}
