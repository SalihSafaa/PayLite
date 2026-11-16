using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace PayLite.WalletApi;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly bool _secureCookie;

    public AuthController(IUserService userService, IOptions<JwtOptions> jwtOptions)
    {
        _userService = userService;
        _secureCookie = jwtOptions.Value.RefreshCookieSecure;
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserResponseDto>> Register(RegisterRequestDto request)
    {
        var result = await _userService.RegisterAsync(request);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Created("api/auth/register", result.Value);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request)
    {
        var result = await _userService.LoginAsync(request);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        RefreshTokenCookie.Write(Response, result.Value!.RefreshToken, _secureCookie);
        return Ok(result.Value);
    }

    /// <summary>
    /// New access token. The refresh token comes from the body or, for the browser, from the httpOnly cookie.
    /// It is rotated on every use, and the new one is written back to the cookie.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponseDto>> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequestDto? request)
    {
        var refreshToken = request?.RefreshToken ?? RefreshTokenCookie.Read(Request);
        if (refreshToken is null)
            return this.ToActionResult(Result<LoginResponseDto>.Failure(ErrorType.UnAuthorized, "No refresh token."));

        var result = await _userService.RefreshAsync(refreshToken);
        if (!result.IsSuccess)
        {
            // Expired, revoked or reused: the cookie is useless now, so remove it.
            RefreshTokenCookie.Delete(Response, _secureCookie);
            return this.ToActionResult(result);
        }

        RefreshTokenCookie.Write(Response, result.Value!.RefreshToken, _secureCookie);
        return Ok(result.Value);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequestDto? request)
    {
        var refreshToken = request?.RefreshToken ?? RefreshTokenCookie.Read(Request);
        if (refreshToken is not null)
            await _userService.LogoutAsync(refreshToken);

        RefreshTokenCookie.Delete(Response, _secureCookie);
        return NoContent();
    }

    [HttpPut("user")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(UserUpdateDto request)
    {
        var result = await _userService.UserUpdate(request);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }
}