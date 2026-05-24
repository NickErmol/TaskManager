#!/bin/bash
# Creates the three application databases inside the shared postgres container.
# Names come from POSTGRES_MULTIPLE_DATABASES (comma-separated).

set -e

if [ -n "$POSTGRES_MULTIPLE_DATABASES" ]; then
    for db in $(echo "$POSTGRES_MULTIPLE_DATABASES" | tr ',' ' '); do
        echo "Creating database: $db"
        psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
            SELECT 'CREATE DATABASE $db'
            WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOSQL
    done
fi
