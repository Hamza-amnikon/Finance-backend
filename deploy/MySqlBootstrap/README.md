# Fresh MySQL database bootstrap

`mysql-schema-baseline.sql` is the reviewed schema of the existing `SparkFinance` MySQL database as of 27 November 2026, plus the `finance__OperationLocks` table for MySQL transaction locks: 73 tables, 44 foreign keys, and the `sales__BranchMasters` compatibility view. It contains no rows or credentials. The old EF migrations in the service projects target SQL Server and must not be run against MySQL.

`MySqlBootstrap` applies the baseline to an **empty, separate MySQL database** and copies only the existing sign-in and company setup rows from the source database: companies, branches, roles, permissions, users, role permissions, tax types, currencies, payment modes, chart of accounts, bank accounts, payment-banking settings, and organization profiles. It does not copy transactions. It refuses a nonempty target or a target with the same name as the source. The target database and its grants must already exist. If a statement fails, inspect and recreate the partially built target before retrying; MySQL DDL cannot be rolled back as a unit.

From the repository root on the MERIT application server:

```bash
/home/hamza/.local/share/merit-host/dotnet/dotnet run \
  --project deploy/MySqlBootstrap/MySqlBootstrap.csproj -- \
  /home/hamza/.local/share/merit-host/client-service.env SparkFinanceStaging
```

Before switching production, back up `SparkFinance`, test every API against the new database, and update each service's database configuration. Do not drop the old database until the new system and rollback path have been verified.
