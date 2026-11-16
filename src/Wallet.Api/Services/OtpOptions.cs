namespace PayLite.WalletApi;

/// <summary>
/// One-time codes for sensitive actions (sending money, revealing the card details).
/// </summary>
/// <remarks>
/// DEMO ONLY: there is no SMS or authenticator app, so the code is a fixed value from configuration
/// (123456 by default). The checks, the error responses and the frontend flow are the real ones; a real
/// deployment would replace <see cref="OtpVerifier"/> with one that checks a code sent to the user.
/// </remarks>
public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    /// <summary>The request header that carries the code.</summary>
    public const string HeaderName = "X-OTP-Code";

    /// <summary>Problem type returned when the code is missing or wrong, so the UI can tell it apart from other 403s.</summary>
    public const string ProblemType = "urn:paylite:otp-invalid";

    public string DemoCode { get; set; } = "123456";
}
