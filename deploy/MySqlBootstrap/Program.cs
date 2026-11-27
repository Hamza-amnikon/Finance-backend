using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

if (args.Length != 2 || !Regex.IsMatch(args[1], "^[A-Za-z][A-Za-z0-9_]{0,63}$"))
{
    Console.Error.WriteLine("Usage: MySqlBootstrap <MERIT environment file> <empty target database>");
    return 2;
}

var line = File.ReadLines(args[0]).Single(value => value.StartsWith("ConnectionStrings__DefaultConnection=", StringComparison.Ordinal));
var raw = line.Split('=', 2)[1].Trim('\'', '"');
var sourceSettings = new MySqlConnectionStringBuilder(raw);
var sourceName = sourceSettings.Database;
var targetName = args[1];
if (string.IsNullOrWhiteSpace(sourceName) || string.Equals(sourceName, targetName, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Source and target databases must be different.");
    return 2;
}

var targetSettings = new MySqlConnectionStringBuilder(raw) { Database = targetName };
await using var connection = new MySqlConnection(targetSettings.ConnectionString);
await connection.OpenAsync();
await using (var count = connection.CreateCommand())
{
    count.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'";
    if (Convert.ToInt32(await count.ExecuteScalarAsync()) != 0)
    {
        Console.Error.WriteLine("Target database must be empty. No changes made.");
        return 2;
    }
}

var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "mysql-schema-baseline.sql"));
var definitions = Regex.Split(sql, @"(?=^CREATE (?:TABLE|VIEW) )", RegexOptions.Multiline).Skip(1)
    .Select(part => part.Replace("SET FOREIGN_KEY_CHECKS=1;", "", StringComparison.Ordinal).Trim().TrimEnd(';')).ToArray();
if (definitions.Length != 74)
{
    throw new InvalidOperationException("The reviewed baseline must contain 73 tables and one view.");
}

var setupTables = new[]
{
    "expenses__Companies", "amnikon__Branches", "security__Roles", "security__Permissions",
    "security__Users", "security__RolePermissions", "accounting__TaxTypes",
    "sales__CurrencyMasters", "sales__PaymentModes", "accounting__Accounts",
    "expenses__BankAccounts", "banking__PaymentBankingSettings", "sales__OrganizationProfiles"
};

await ExecuteAsync("SET FOREIGN_KEY_CHECKS=0");
try
{
    foreach (var definition in definitions)
    {
        await ExecuteAsync(definition);
    }

    foreach (var table in setupTables)
    {
        var columns = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT COLUMN_NAME FROM information_schema.columns
                WHERE table_schema=DATABASE() AND table_name=@name
                  AND EXTRA NOT LIKE '%GENERATED%'
                ORDER BY ORDINAL_POSITION
                """;
            command.Parameters.AddWithValue("@name", table);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add($"`{reader.GetString(0).Replace("`", "``", StringComparison.Ordinal)}`");
            }
        }

        if (columns.Count == 0)
        {
            throw new InvalidOperationException($"Setup table {table} is missing from the baseline.");
        }

        var list = string.Join(',', columns);
        var source = $"`{sourceName.Replace("`", "``", StringComparison.Ordinal)}`";
        await ExecuteAsync($"INSERT INTO `{targetName}`.`{table}` ({list}) SELECT {list} FROM {source}.`{table}`");
    }
}
finally
{
    await ExecuteAsync("SET FOREIGN_KEY_CHECKS=1");
}

Console.WriteLine($"Created 73 MySQL tables, one view, and copied {setupTables.Length} setup tables into {targetName}.");
return 0;

async Task ExecuteAsync(string statement)
{
    await using var command = connection.CreateCommand();
    command.CommandText = statement;
    await command.ExecuteNonQueryAsync();
}
