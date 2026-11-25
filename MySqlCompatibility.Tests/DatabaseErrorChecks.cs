using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using SparkFinance.Database;

internal static class DatabaseErrorChecks
{
    public static void Run(Action<bool, string> check)
    {
        var duplicate = Create(1062);
        check(DatabaseErrors.IsDuplicateKey(duplicate), "MySQL duplicate keys are recognized");
        check(DatabaseErrors.IsDuplicateKey(new DbUpdateException("Save failed", duplicate)),
            "EF-wrapped MySQL duplicates retain idempotent recovery classification");
        check(!DatabaseErrors.IsDuplicateKey(Create(1064)), "Invalid SQL is not treated as a duplicate request");
        check(DatabaseErrors.IsDeadlock(Create(1213)), "MySQL deadlocks are classified separately");
        check(DatabaseErrors.IsLockTimeout(Create(1205)) && !DatabaseErrors.IsDeadlock(Create(1205)),
            "MySQL lock waits do not inherit SQL Server's deadlock error number");
        check(DatabaseErrors.IsForeignKeyViolation(Create(1451)) && DatabaseErrors.IsForeignKeyViolation(Create(1452)),
            "MySQL foreign-key restrictions are recognized for parent and child operations");
        check(!DatabaseErrors.IsDuplicateKey(new OperationCanceledException()), "Cancellation cannot trigger duplicate-request recovery");
    }

    internal static MySqlException Create(int number)
    {
        // Construct a driver exception without opening a connection.
        var constructors = typeof(MySqlException).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(int) || parameter.ParameterType == typeof(MySqlErrorCode)))
            .OrderBy(constructor => constructor.GetParameters().Length);
        foreach (var constructor in constructors)
        {
            var arguments = constructor.GetParameters().Select(parameter =>
                parameter.ParameterType == typeof(int) ? (object)number :
                parameter.ParameterType == typeof(MySqlErrorCode) ? Enum.ToObject(typeof(MySqlErrorCode), number) :
                parameter.ParameterType == typeof(string) ? "HY000" :
                parameter.ParameterType == typeof(bool) ? false : null).ToArray();
            try
            {
                if (constructor.Invoke(arguments) is MySqlException exception && exception.Number == number)
                {
                    return exception;
                }
            }
            catch (TargetInvocationException)
            {
                // Some overloads require internal driver state; use another overload.
            }
        }
        throw new InvalidOperationException("Could not create the isolated MySQL error fixture.");
    }
}
