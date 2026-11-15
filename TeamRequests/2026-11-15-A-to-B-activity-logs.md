# Activity Service logs: correlation id prefix and noise

Status: Open
From: Salih (A) · To: Aboalhassan (B)

From the compose run (see `2026-11-15-A-to-B-runbook-scenario-4.md`). Two things that will show
when we put the logs on screen on Demo Day:

1. **Correlation id on every line.** The brief asks for it on every log line. You already push it
   with `BeginScope` in `TransferEventProcessor`, but the console output doesn't print it, so only
   the "Processed event" message shows it (inside the text). On the Wallet side the Serilog template
   is `"[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"`
   with `.Enrich.FromLogContext()`, so every line starts with `[<id>]`.
2. **Noise while Kafka is down.** Your service printed **1,156** raw `%3|...|ERROR|rdkafka...` lines
   in 3 minutes, plus the full SQL of every command. On the Wallet side I fixed the same thing in this
   PR:
   - `.SetLogHandler((_, log) => _logger.LogDebug("librdkafka {Facility}: {Message}", log.Facility, log.Message))`
     on the Kafka builders, so librdkafka goes through our logger instead of stderr;
   - `.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)` and
     `.MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)`
     in the Serilog setup (Serilog doesn't read the `Logging` section of appsettings).

Not urgent compared to the DLT + retry limit (scenario 7), but both are small.
