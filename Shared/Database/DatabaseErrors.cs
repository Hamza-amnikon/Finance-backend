using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;

namespace SparkFinance.Database
{
    public static class DatabaseErrors
    {
        public static bool IsDuplicateKey(Exception exception)
        {
            return Matches(exception, [2601, 2627], [1062]);
        }

        public static bool IsForeignKeyViolation(Exception exception)
        {
            return Matches(exception, [547], [1451, 1452]);
        }

        public static bool IsDeadlock(Exception exception)
        {
            return Matches(exception, [1205], [1213]);
        }

        public static bool IsLockTimeout(Exception exception)
        {
            return Matches(exception, [1222], [1205]);
        }

        private static bool Matches(Exception exception, int[] sqlServerNumbers, int[] mySqlNumbers)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlServer && sqlServerNumbers.Contains(sqlServer.Number))
                {
                    return true;
                }
                if (current is MySqlException mySql && mySqlNumbers.Contains(mySql.Number))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
