using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace PayLite.WalletApi;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
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

        return Ok(result.Value);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponseDto>> Refresh(RefreshRequestDto request)
    {
        var result = await _userService.RefreshAsync(request.RefreshToken);
        if (!result.IsSuccess)
            return this.ToActionResult(result);

        return Ok(result.Value);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequestDto request)
    {
        await _userService.LogoutAsync(request.RefreshToken);
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