using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PayLite.Contracts;

namespace PayLite.ActivityService;

/// <summary>
/// Applies a transfer event and all its ActivityDb effects in one transaction.
/// </summary>
public class TransferEventProcessor
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    private static readonly TimeZoneInfo Baghdad = FindBaghdad();

    private readonly ActivityDbContext _context;
    private readonly ILogger<TransferEventProcessor> _logger;

    public TransferEventProcessor(ActivityDbContext context, ILogger<TransferEventProcessor> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ProcessOutcome> ProcessAsync(string? json, CancellationToken cancellationToken)
    {
        var envelope = Parse(json);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["EventId"] = envelope.EventId,
            ["CorrelationId"] = envelope.CorrelationId
        });

        if (envelope.EventType != EventTypes.TransferCompleted)
        {
            _logger.LogInformation(
                "Ignoring event {EventId} of type {EventType} (correlation {CorrelationId})",
                envelope.EventId,
                envelope.EventType,
                envelope.CorrelationId);
            return ProcessOutcome.IgnoredEventType;
        }

        var data = ReadTransferCompleted(envelope);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _context.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = envelope.EventId,
                EventType = envelope.EventType,
                ProcessedAt = DateTime.UtcNow
            });

            _context.Notifications.Add(new Notification
            {
                UserId = data.ToUserId,
                EventId = envelope.EventId,
                TransferId = data.TransferId,
                FromUsername = data.FromUsername,
                Amount = data.Amount,
                Currency = data.Currency,
                OccurredAt = envelope.OccurredAt,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogInformation(
                "Event {EventId} was already processed, skipping (correlation {CorrelationId})",
                envelope.EventId,
                envelope.CorrelationId);
            return ProcessOutcome.Duplicate;
        }

        if (data.ToIsMerchant)
        {
            await AddToMerchantSummaryAsync(data, envelope.OccurredAt, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Processed event {EventId}: transfer {TransferId}, {Amount} {Currency} to user {ToUserId} (correlation {CorrelationId})",
            envelope.EventId,
            data.TransferId,
            data.Amount,
            data.Currency,
            data.ToUserId,
            envelope.CorrelationId);

        return ProcessOutcome.Processed;
    }

    private static EventEnvelope<JsonElement> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new PoisonMessageException("The message has no value.");
        }

        EventEnvelope<JsonElement>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope<JsonElement>>(json, EventJson.Options);
        }
        catch (JsonException ex)
        {
            throw new PoisonMessageException("The message is not valid JSON.", ex);
        }

        if (envelope is null || envelope.EventId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.EventType))
        {
            throw new PoisonMessageException("The envelope has no eventId or eventType.");
        }

        return envelope;
    }

    private static TransferCompletedData ReadTransferCompleted(EventEnvelope<JsonElement> envelope)
    {
        if (envelope.Version != TransferCompletedData.CurrentVersion)
        {
            throw new PoisonMessageException(
                $"Unknown {EventTypes.TransferCompleted} version {envelope.Version}.");
        }

        if (envelope.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new PoisonMessageException("TransferCompleted v1 payload is missing.");
        }

        TransferCompletedData? data;
        try
        {
            data = envelope.Data.Deserialize<TransferCompletedData>(EventJson.Options);
        }
        catch (JsonException ex)
        {
            throw new PoisonMessageException("The data does not match TransferCompleted v1.", ex);
        }

        if (data is null ||
            data.TransferId <= 0 ||
            data.FromWalletId <= 0 ||
            data.FromUserId <= 0 ||
            data.ToWalletId <= 0 ||
            data.ToUserId <= 0 ||
            data.Amount <= 0 ||
            string.IsNullOrWhiteSpace(data.FromUsername) ||
            data.FromUsername.Length > 50 ||
            data.Currency is not { Length: 3 })
        {
            throw new PoisonMessageException(
                "TransferCompleted v1 is missing a required field or has an invalid amount.");
        }

        return data;
    }

    private async Task AddToMerchantSummaryAsync(
        TransferCompletedData data,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var utc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, Baghdad);
        var date = DateOnly.FromDateTime(local);
        var hour = (byte)local.Hour;

        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE activity.MerchantHourlySummaries WITH (UPDLOCK, SERIALIZABLE)
            SET PaymentCount = PaymentCount + 1,
                TotalAmount = TotalAmount + {data.Amount},
                LastUpdatedAt = SYSUTCDATETIME()
            WHERE MerchantUserId = {data.ToUserId} AND [Date] = {date} AND [Hour] = {hour};

            IF @@ROWCOUNT = 0
                INSERT INTO activity.MerchantHourlySummaries
                    (MerchantUserId, [Date], [Hour], PaymentCount, TotalAmount, Currency, LastUpdatedAt)
                VALUES ({data.ToUserId}, {date}, {hour}, 1, {data.Amount}, {data.Currency}, SYSUTCDATETIME());
            """, cancellationToken);
    }

    private static bool IsDuplicate(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: PrimaryKeyViolation or UniqueIndexViolation };

    private static TimeZoneInfo FindBaghdad() =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Baghdad", out var zone) && zone is not null
            ? zone
            : TimeZoneInfo.CreateCustomTimeZone(
                "Asia/Baghdad",
                TimeSpan.FromHours(3),
                "Baghdad",
                "Baghdad");
}
