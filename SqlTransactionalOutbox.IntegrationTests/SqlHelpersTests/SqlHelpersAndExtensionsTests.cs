using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlTransactionalOutbox.Tests;
using SqlTransactionalOutbox.CustomExtensions;
using System;
using System.Threading.Tasks;

namespace SqlTransactionalOutbox.IntegrationTests.SqlHelpersTests
{
    [TestClass]
    public class SqlHelpersAndExtensionsTests
    {
        // Pseudocode-ish example; adapt to your test framework and provider
        [TestMethod]
        public async Task TestSqlReaderGetValueSafelyExtensionWorksForCommonTypes()
        {
            await using var sqlConnection = await SqlConnectionHelper.CreateMicrosoftDataSqlConnectionAsync().ConfigureAwait(false);

            using var sqlCmd = sqlConnection.CreateCommand();
            sqlCmd.CommandText = @"
                SELECT
                    CAST('hello' AS nvarchar(50)) AS S,
                    CAST('2024-01-02T03:04:05Z' AS datetime2) AS DT,
                    CAST(NULL AS datetime2) AS DTNULL,
                    CAST(1 AS bit) AS B,
                    CAST(42 AS int) AS I,
                    CAST(123.45 AS decimal(10,2)) AS M,
                    CAST(0x010203 AS varbinary(10)) AS BYTES
            ";

            using var sqlReader = await sqlCmd.ExecuteReaderAsync();

            Assert.IsTrue(await sqlReader.ReadAsync());

            // string
            Assert.AreEqual("hello", sqlReader.GetValueSafely<string>(sqlReader.GetOrdinal("S")));

            // DateTime and DateTime?
            var dt = sqlReader.GetValueSafely<DateTime>(sqlReader.GetOrdinal("DT"));
            var dtNullable = sqlReader.GetValueSafely<DateTime?>(sqlReader.GetOrdinal("DT"));
            Assert.AreEqual(dt, dtNullable);

            // NULL handling for nullable
            Assert.IsNull(sqlReader.GetValueSafely<DateTime?>(sqlReader.GetOrdinal("DTNULL")));

            // NULL handling for non-nullable should throw
            Assert.ThrowsException<InvalidOperationException>(() => sqlReader.GetValueSafely<DateTime>(sqlReader.GetOrdinal("DTNULL")));

            // bool, int, decimal, byte[]
            Assert.AreEqual(true, sqlReader.GetValueSafely<bool>(sqlReader.GetOrdinal("B")));
            Assert.AreEqual(42, sqlReader.GetValueSafely<int>(sqlReader.GetOrdinal("I")));
            Assert.AreEqual(123.45m, sqlReader.GetValueSafely<decimal>(sqlReader.GetOrdinal("M")));
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, sqlReader.GetValueSafely<byte[]>(sqlReader.GetOrdinal("BYTES")));
        }
    }
}
