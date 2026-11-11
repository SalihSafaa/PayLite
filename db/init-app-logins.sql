-- Runs once at setup, as sa, AFTER both migrators. Safe to run again.
-- Each service gets its own login that can only touch its own database:
--   wallet_app   -> WalletDb only    (read, write, EXECUTE on the wallet schema's procedures)
--   activity_app -> ActivityDb only  (read, write)
-- So the Activity Service physically cannot read WalletDb, which the architecture forbids anyway.

-- ---------- Wallet ----------
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'wallet_app')
    CREATE LOGIN wallet_app WITH PASSWORD = N'$(WALLET_DB_PASSWORD)', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN wallet_app WITH PASSWORD = N'$(WALLET_DB_PASSWORD)';
GO
USE WalletDb;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'wallet_app')
    CREATE USER wallet_app FOR LOGIN wallet_app;
ALTER ROLE db_datareader ADD MEMBER wallet_app;
ALTER ROLE db_datawriter ADD MEMBER wallet_app;
-- Granted on the SCHEMA, so procedures added later (wallet.usp_Transfer, ...) are covered
-- without editing this script.
GRANT EXECUTE ON SCHEMA::wallet TO wallet_app;
GO

-- ---------- Activity ----------
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'activity_app')
    CREATE LOGIN activity_app WITH PASSWORD = N'$(ACTIVITY_DB_PASSWORD)', CHECK_POLICY = ON;
ELSE
    ALTER LOGIN activity_app WITH PASSWORD = N'$(ACTIVITY_DB_PASSWORD)';
GO
USE ActivityDb;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'activity_app')
    CREATE USER activity_app FOR LOGIN activity_app;
ALTER ROLE db_datareader ADD MEMBER activity_app;
ALTER ROLE db_datawriter ADD MEMBER activity_app;
GRANT EXECUTE ON SCHEMA::activity TO activity_app;
GO
