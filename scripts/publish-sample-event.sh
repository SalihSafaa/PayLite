#!/usr/bin/env bash
# Development only: put test messages on the paylite.transfers topic of the local compose stack.
# Used for the failure demos and for testing the Activity Service without making real transfers.
#
#   scripts/publish-sample-event.sh replay [outbox-id]   # scenario 5: send an event that was already sent
#                                                       #   (default: the newest sent outbox row)
#   scripts/publish-sample-event.sh poison              # scenario 7: send a message that is not valid JSON
#   scripts/publish-sample-event.sh sample <toUserId> <toWalletId> <amount> [merchant]
#                                                       # a new, valid TransferCompleted event (no money moves)
#
# Run it from the repo root while `docker compose up` is running. It reads DB_PASSWORD from .env.
set -euo pipefail

TOPIC="paylite.transfers"

cd "$(dirname "$0")/.."
if [[ ! -f .env ]]; then
  echo "No .env found. Run this from a checkout where docker compose is set up." >&2
  exit 1
fi
DB_PASSWORD="$(grep -E '^DB_PASSWORD=' .env | head -n1 | cut -d= -f2-)"

usage() {
  sed -n '5,9p' "$0" | sed 's/^# \{0,1\}//'
  exit 1
}

# Runs a query against WalletDb as sa and prints the raw result (no headers, no row count).
sql() {
  docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C \
    -d WalletDb -h -1 -y 8000 -Q "SET NOCOUNT ON; $1"
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
    id="${2:-}"
    if [[ -z "$id" ]]; then
      id="$(sql "SELECT TOP (1) Id FROM wallet.OutboxMessages WHERE SentAt IS NOT NULL ORDER BY Id DESC" | tr -d '[:space:]')"
      [[ -n "$id" ]] || { echo "No sent outbox rows yet. Make a transfer first." >&2; exit 1; }
    fi
    [[ "$id" =~ ^[0-9]+$ ]] || { echo "Outbox id must be a number." >&2; exit 1; }
    key="$(sql "SELECT MessageKey FROM wallet.OutboxMessages WHERE Id = $id" | tr -d '[:space:]')"
    [[ -n "$key" ]] || { echo "Outbox row $id not found." >&2; exit 1; }
    correlation="$(sql "SELECT CorrelationId FROM wallet.OutboxMessages WHERE Id = $id" | tr -d '[:space:]')"
    payload="$(sql "SELECT Payload FROM wallet.OutboxMessages WHERE Id = $id" | tr -d '\r\n' | sed 's/[[:space:]]*$//')"
    event_id="$(sql "SELECT EventId FROM wallet.OutboxMessages WHERE Id = $id" | tr -d '[:space:]')"
    produce "X-Correlation-ID:$correlation,event-type:TransferCompleted" "$key" "$payload"
    echo "Re-sent outbox row $id (event $event_id, correlation $correlation)."
    echo "Expected: the Activity Service logs 'already processed, skipping' and no second notification appears."
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
