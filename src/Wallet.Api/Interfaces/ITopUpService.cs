namespace PayLite.WalletApi;

public interface ITopUpService
{
    /// <summary>Creates a Pending request. Nothing is credited until an Admin approves it.</summary>
    Task<Result<TopUpRequestDto>> RequestAsync(int userId, CreateTopUpDto request);

    /// <summary>The caller's own requests, newest first.</summary>
    Task<Result<List<TopUpRequestDto>>> GetMineAsync(int userId, int limit);

    /// <summary>For Admins: requests with this status (null = all); Pending oldest first, the rest newest first.</summary>
    Task<Result<List<TopUpRequestDto>>> ListAsync(string? status, int limit);

    Task<Result<TopUpRequestDto>> ApproveAsync(int topUpId, int adminUserId, string adminUsername);
    Task<Result<TopUpRequestDto>> RejectAsync(int topUpId, int adminUserId, string adminUsername);
}
