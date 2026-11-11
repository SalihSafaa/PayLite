namespace PayLite.WalletApi;

public interface IUserService
{
    Task<Result<UserResponseDto>> RegisterAsync(RegisterRequestDto request);
    Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request);
    Task<Result<LoginResponseDto>> RefreshAsync(string refreshToken);
    Task<Result<UserResponseDto>> UserUpdate(UserUpdateDto request);
    Task LogoutAsync(string refreshToken);
}