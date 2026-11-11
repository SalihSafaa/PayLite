using Microsoft.EntityFrameworkCore;

namespace PayLite.WalletApi;

public class UserService : IUserService
{
    private readonly WalletDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<UserService> _logger;

    public UserService(
        WalletDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        ILogger<UserService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<Result<UserResponseDto>> RegisterAsync(RegisterRequestDto request)
    {
        var username = request.Username.Trim();
        var usernameExists = await _context.Users
            .AnyAsync(user => user.Username == username); //removed the toupper replaced with collation in db

        if (usernameExists)
        {
            return Result<UserResponseDto>.Failure(ErrorType.Conflict, $"Username '{username}' is already in use. Please choose a different username.");
        }

        var user = new User
        {
            Username = username,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = Roles.User
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered with ID: {UserId}", user.Id);

        return Result<UserResponseDto>.Success(ToResponse(user));
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var username = request.Username.Trim();
        var user = await _context.Users
            .SingleOrDefaultAsync(candidate => candidate.Username == username);//replaced with collation

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", username);
            return Result<LoginResponseDto>.Failure(ErrorType.UnAuthorized, "Invalid username or password. Please check your credentials and try again.");
        }

        var refreshToken = await _refreshTokenService.GenerateAsync(user);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = _jwtTokenService.CreateToken(user),
            RefreshToken = refreshToken
        });
    }

    public async Task<Result<LoginResponseDto>> RefreshAsync(string refreshToken)
    {
        var result = await _refreshTokenService.ValidateAndRotateAsync(refreshToken);
        if (!result.IsSuccess)
        {
            return Result<LoginResponseDto>.Failure(result.ErrorType, result.ErrorMessage!);
        }

        var (user, newRawRefreshToken) = result.Value;

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            AccessToken = _jwtTokenService.CreateToken(user),
            RefreshToken = newRawRefreshToken
        });
    }

    public async Task LogoutAsync(string refreshToken)
    {
        await _refreshTokenService.RevokeAsync(refreshToken);
    }

    public async Task<Result<UserResponseDto>> UserUpdate(UserUpdateDto userUpdateDto)
    {
        if (string.IsNullOrWhiteSpace(userUpdateDto.Role))
        {
            return Result<UserResponseDto>.Failure(ErrorType.Validation, "Role is required.");
        }

        var role = userUpdateDto.Role.Trim();
        var knownRole = new[] { Roles.User, Roles.Merchant, Roles.Admin }
            .FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        if (knownRole is null)
        {
            return Result<UserResponseDto>.Failure(ErrorType.Validation, $"Role must be one of: {Roles.User}, {Roles.Merchant}, {Roles.Admin}.");
        }
        role = knownRole;

        var user = await _context.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userUpdateDto.Id);

        if (user is null)
        {
            return Result<UserResponseDto>.Failure(ErrorType.NotFound, "User was not found.");
        }

        user.Role = role;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User updated with ID: {UserId}", user.Id);

        return Result<UserResponseDto>.Success(ToResponse(user));
    }

    private static UserResponseDto ToResponse(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role,
        CreatedAt = user.CreatedAt,
        LastUpdate = user.LastUpdate,
        CreatedBy = user.CreatedBy,
        LastUpdatedBy = user.LastUpdatedBy
    };
}