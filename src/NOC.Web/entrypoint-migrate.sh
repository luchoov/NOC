#!/bin/sh
set -eu

if [ -n "${PGHOST:-}" ] && [ -n "${PGDATABASE:-}" ] && [ -n "${PGUSER:-}" ] && [ -n "${PGPASSWORD:-}" ]; then
    echo "Normalizing __EFMigrationsHistory columns if needed..."
    psql \
        -v ON_ERROR_STOP=1 \
        -h "${PGHOST}" \
        -p "${PGPORT:-5432}" \
        -U "${PGUSER}" \
        -d "${PGDATABASE}" <<'SQL'
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = '__EFMigrationsHistory'
    ) THEN
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = '__EFMigrationsHistory'
              AND column_name = 'MigrationId'
        ) THEN
            EXECUTE 'ALTER TABLE public."__EFMigrationsHistory" RENAME COLUMN "MigrationId" TO migration_id';
        END IF;

        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = '__EFMigrationsHistory'
              AND column_name = 'ProductVersion'
        ) THEN
            EXECUTE 'ALTER TABLE public."__EFMigrationsHistory" RENAME COLUMN "ProductVersion" TO product_version';
        END IF;
    END IF;
END $$;
SQL
else
    echo "Skipping __EFMigrationsHistory normalization because PostgreSQL connection env vars are incomplete."
fi

exec dotnet ef database update \
    --project src/NOC.Web/NOC.Web.csproj \
    --startup-project src/NOC.Web/NOC.Web.csproj \
    --configuration Release \
    --no-build
