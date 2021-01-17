using System;
using System.Collections.Generic;
using System.Text;
using SqlTransactionalOutboxHelpers;
using SqlTransactionalOutboxHelpers.Tests;
using SystemData = System.Data.SqlClient;
//using MicrosoftData = Microsoft.Data.SqlClient;

namespace SqlTransactionalOutboxHelpers.Tests
{
    public class SqlConnectionHelper
    {
        public static SystemData.SqlConnection CreateSystemDataSqlConnection()
        {
            return new SystemData.SqlConnection(TestConfiguration.SqlConnectionString);
        }

        //public static MicrosoftData.SqlConnection CreateMicrosoftDataSqlConnection()
        //{
        //    return new MicrosoftData.SqlConnection(TestConfiguration.SqlConnectionString);
        //}
    }
}
