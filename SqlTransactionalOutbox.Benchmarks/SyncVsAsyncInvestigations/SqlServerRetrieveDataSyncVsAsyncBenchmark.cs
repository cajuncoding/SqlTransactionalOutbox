//using System;
//using System.Collections.Generic;
//using System.Data.SqlClient;
//using System.Text;
//using System.Threading.Tasks;
//using BenchmarkDotNet.Attributes;
//using SqlTransactionalOutbox.IntegrationTests;
//using SqlTransactionalOutbox.Tests;
//using SqlTransactionalOutbox.SqlServer.SystemDataNS;
//using Guid = System.Guid;

//namespace SqlTransactionalOutbox.Benchmarks
//{
//    public class SqlServerRetrieveDataSyncVsAsyncBenchmark
//    {
//        [Params(10, 20)]
//        public int N { get; set; }

//        [Params(10, 500)]
//        public int DataSize { get; set; }

//        [IterationSetup]
//        public void Setup()
//        {
//            Task.Run(async () =>
//            {
//                //*****************************************************************************************
//                //* STEP 1 - Prepare/Clear the Queue Table
//                //*****************************************************************************************
//                await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
//                await SystemDataSqlTestHelpers.PopulateTransactionalOutboxTestDataAsync(DataSize);
//            }).GetAwaiter().GetResult();
//        }

//        [Benchmark]
//        public async Task<int> TrueAsyncOriginal()
//        {
//            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
//            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync();

//            var outboxRepo = new SqlServerGuidTransactionalOutboxRepository<string>(sqlTransaction);

//            return await RunBenchmarkForImplementation(outboxRepo);
//        }

//        [Benchmark]
//        public async Task<int> SyncOverAsync()
//        {
//            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
//            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync();

//            var outboxRepo = new SqlServerSyncOverAsyncGuidTransactionalOutboxRepository<string>(sqlTransaction);

//            return await RunBenchmarkForImplementation(outboxRepo);
//        }

//        [Benchmark]
//        public async Task<int> TrueAsyncOptimized()
//        {
//            await using var sqlConnection = await SqlConnectionHelper.CreateSystemDataSqlConnectionAsync();
//            await using var sqlTransaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync();

//            var outboxRepo = new SqlServerAsyncOptimizeExperimentsTransactionalOutboxRepository<string>(sqlTransaction);

//            return await RunBenchmarkForImplementation(outboxRepo);
//        }


//        private async Task<int> RunBenchmarkForImplementation(ISqlTransactionalOutboxRepository<Guid, string> outboxRepo)
//        {
//            //Retrieve the data...
//            var results = await outboxRepo.RetrieveOutboxItemsAsync(OutboxItemStatus.Pending);

//            if (results.Count != DataSize)
//                throw new Exception($"Results count [{results.Count}] did not match DataSize [{DataSize}]!");

//            //Return something so that the Results must be computed and can't be JIT eliminated due to no usage...
//            return results.Count;
//        }
//    }
//}
