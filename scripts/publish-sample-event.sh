#!/usr/bin/env bash
# Development only: put test messages on the paylite.transfers topic of the local compose stack.
# Used for the failure demos and for testing the Activity Service without making real transfers.
#
#   scripts/publish-sample-event.sh replay [outbox-id]   # scenario 5: the outbox publishes a sent event again
#                                                       #   (default: the newest sent outbox row)
#   scripts/publish-sample-event.sh poison              # scenario 7: send a message that is not valid JSON
#   scripts/publish-sample-event.sh sample <toUserId> <toWalletId> <amount> [merchant]
#                                                       # a new, valid TransferCompleted event (no money moves)
#
# Run it from anywhere in the repo while `docker compose up` is running.
set -euo pipefail

TOPIC="paylite.transfers"

cd "$(dirname "$0")/.."

usage() {
  sed -n '5,9p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

# Runs a query against WalletDb as sa and prints the raw result (no headers, no row count).
# The password is read inside the container, so it doesn't need to be exported in your shell.
# -I: QUOTED_IDENTIFIER ON, which SQL Server requires to UPDATE a table with a filtered index (the outbox has one).
# -b: exit with an error if the query fails, so the script stops instead of printing the error as data.
sql() {
  docker compose exec -T -e QUERY="SET NOCOUNT ON; $1" sqlserver bash -c \
    '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -I -b -d WalletDb -h -1 -W -Q "$QUERY"'
}

# Produces one message. $1 = headers ("name:value,name:value"), $2 = key, $3 = value (one line).
# Header values must not contain ',' or ':'; the value may contain anything except a tab or newline.
produce() {
  printf '%s\t%s|%s\n' "$1" "$2" "$3" | docker compose exec -T kafka /opt/kafka/bin/kafka-console-producer.sh \
    --bootstrap-server localhost:9092 --topic "$TOPIC" \
    --property parse.headers=true --property parse.key=true --property 'key.separator=|'
}

new_uuid() { cat /proc/sys/kernel/random/uuid; }

case "${1:-}" in
  replay)
    # Like a publisher crash after Kafka acknowledged the message but before SentAt was set (ADR-0004):
    # clear SentAt and let the real OutboxPublisher send the same eventId, key and headers again.
    id="${2:-}"
    if [[ -z "$id" ]]; then
      id="$(sql "SELECT TOP (1) Id FROM wallet.OutboxMessages WHERE SentAt IS NOT NULL ORDER BY Id DESC")"
      [[ -n "$id" ]] || { echo "No sent outbox rows yet. Make a transfer first." >&2; exit 1; }
    fi
    [[ "$id" =~ ^[0-9]+$ ]] || { echo "Outbox id must be a number." >&2; exit 1; }
    event_id="$(sql "UPDATE wallet.OutboxMessages SET SentAt = NULL OUTPUT inserted.EventId WHERE Id = $id AND SentAt IS NOT NULL")"
    [[ -n "$event_id" ]] || { echo "Outbox row $id not found, or not sent yet." >&2; exit 1; }
    echo "Outbox row $id (event $event_id) will be published again within about a second."
    echo "Expected: wallet-api logs 'Published event' again; the Activity Service logs 'already processed, skipping'."
    ;;

  poison)
    correlation="demo-poison-$(date +%H%M%S)"
    produce "X-Correlation-ID:$correlation,event-type:TransferCompleted" "0" "this is not json"
    echo "Sent an invalid message (correlation $correlation)."
    echo "Expected (ADR-0004): it goes to paylite.transfers.dlt and the consumer keeps going."
    ;;

  sample)
    [[ $# -ge 4 ]] || usage
    to_user="$2"; to_wallet="$3"; amount="$4"; merchant="false"
    [[ "${5:-}" == "merchant" ]] && merchant="true"
    for n in "$to_user" "$to_wallet" "$amount"; do
      [[ "$n" =~ ^[1-9][0-9]*$ ]] || { echo "toUserId, toWalletId and amount must be positive whole numbers." >&2; exit 1; }
    done
    event_id="$(new_uuid)"
    correlation="demo-sample-$(date +%H%M%S)"
    now="$(date -u +%Y-%m-%dT%H:%M:%S.000Z)"
    # transferId 900000000+ so a sample can never be mistaken for a real transfer.
    transfer_id="9$(date +%s | tail -c 9)"
    payload="{\"eventId\":\"$event_id\",\"eventType\":\"TransferCompleted\",\"version\":1,\"occurredAt\":\"$now\",\"correlationId\":\"$correlation\",\"data\":{\"transferId\":$transfer_id,\"fromWalletId\":1,\"fromUserId\":1,\"fromUsername\":\"sample-sender\",\"toWalletId\":$to_wallet,\"toUserId\":$to_user,\"toIsMerchant\":$merchant,\"amount\":$amount,\"currency\":\"IQD\"}}"
    produce "X-Correlation-ID:$correlation,event-type:TransferCompleted" "$to_wallet" "$payload"
    echo "Sent sample event $event_id to user $to_user (wallet $to_wallet), $amount IQD, merchant=$merchant."
    echo "Note: no money moved. This only exercises the Activity Service."
    ;;

  *)
    usage
    ;;
esac
