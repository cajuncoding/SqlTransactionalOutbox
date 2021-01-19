using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SqlTransactionalOutboxHelpers;
using SqlTransactionalOutboxHelpers.Tests;
using SystemData = System.Data.SqlClient;
//using MicrosoftData = Microsoft.Data.SqlClient;

namespace SqlTransactionalOutboxHelpers.Tests
{
    public class SqlConnectionHelper
    {
        public static async Task<SystemData.SqlConnection> CreateSystemDataSqlConnectionAsync()
        {
            var sqlConnection = new SystemData.SqlConnection(TestConfiguration.SqlConnectionString);
            await sqlConnection.OpenAsync();
            return sqlConnection;
        }

        //public static MicrosoftData.SqlConnection CreateMicrosoftDataSqlConnectionAsync()
        //{
        //    return new MicrosoftData.SqlConnection(TestConfiguration.SqlConnectionString);
        //}
    }
}
