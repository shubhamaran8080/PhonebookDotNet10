#!/usr/bin/env bash
#
# Generates realistic dummy contacts in the PostgreSQL database.
#
#   ./scripts/seed-contacts.sh [count]     # default: 1000
#
# Only INSERTs contacts - existing contacts and users are never updated or
# deleted. Phone numbers and emails are guaranteed unique (both against the
# database and within the generated batch).

set -euo pipefail

COUNT="${1:-1000}"

if ! [[ "$COUNT" =~ ^[0-9]+$ ]] || [ "$COUNT" -le 0 ]; then
    echo "Usage: $0 [count]   (count must be a positive integer)" >&2
    exit 1
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE=(docker compose --project-directory "$REPO_ROOT" --project-name phonebook-dotnet10)

contact_count() {
    "${COMPOSE[@]}" exec -T db \
        psql -U phonebook -d phonebook_dotnet10 -t -A -c "SELECT COUNT(*) FROM contacts;"
}

BEFORE="$(contact_count)"
echo "Contacts before: $BEFORE"

# Seed__Exit=true makes the container seed and then shut down instead of
# starting Kestrel, so the already-running backend is not affected.
"${COMPOSE[@]}" run --rm \
    -e "Seed__Count=$COUNT" \
    -e "Seed__Exit=true" \
    backend

AFTER="$(contact_count)"
echo ""
echo "Contacts after:  $AFTER"
echo "Inserted:        $((AFTER - BEFORE))"

if [ "$((AFTER - BEFORE))" -ne "$COUNT" ]; then
    echo "WARNING: expected to insert $COUNT contacts, but the count grew by $((AFTER - BEFORE))." >&2
    exit 1
fi
