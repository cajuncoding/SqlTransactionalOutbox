#nullable disable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SqlServer.MicrosoftDataNS
{
    public class AsyncThreadOutboxProcessingAgent : IAsyncDisposable
    {
        private object _padLock = new object();

        protected string SqlConnectionString { get; }

        protected ISqlTransactionalOutboxPublisher<Guid> SqlTransactionalOutboxPublisher { get; }
        protected OutboxProcessingOptions SqlTransactionalOutboxProcessingOptions { get; }

        protected TimeSpan ProcessingIntervalTimespan { get; }
        protected TimeSpan HistoryToKeepTimespan { get; }

        protected CancellationTokenSource CancellationTokenSource { get; set; }

        protected Task<long> ProcessingTask { get; set; }

        public long ExecutionCount { get; protected set; }

        public AsyncThreadOutboxProcessingAgent(
            TimeSpan processingIntervalTimeSpan,
            string sqlConnectionString,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            OutboxProcessingOptions outboxProcessingOptions = null
        ) : this(processingIntervalTimeSpan, TimeSpan.Zero, sqlConnectionString, outboxPublisher, outboxProcessingOptions)
        { }

        public AsyncThreadOutboxProcessingAgent(
            TimeSpan processingIntervalTimeSpan,
            TimeSpan historyToKeepTimeSpan,
            string sqlConnectionString,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            OutboxProcessingOptions outboxProcessingOptions = null
        )
        {
            this.ProcessingIntervalTimespan = processingIntervalTimeSpan.AssertNotNull(nameof(processingIntervalTimeSpan));
            this.HistoryToKeepTimespan = historyToKeepTimeSpan.AssertNotNull(nameof(historyToKeepTimeSpan));
            this.SqlConnectionString = sqlConnectionString.AssertNotNullOrWhiteSpace(nameof(sqlConnectionString));
            this.SqlTransactionalOutboxPublisher = outboxPublisher.AssertNotNull(nameof(outboxPublisher));
            this.SqlTransactionalOutboxProcessingOptions = outboxProcessingOptions ?? OutboxProcessingOptions.DefaultOutboxProcessingOptions;
        }

        public Task StartAsync()
        {
            this.CancellationTokenSource = new CancellationTokenSource();
            
            var cancellationToken = this.CancellationTokenSource.Token;

            this.ProcessingTask = Task.Run(async () =>
            {
                long executionCount = 0;
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        //Wait for the next iteration to Process the Outbox...
                        await Task.Delay(this.ProcessingIntervalTimespan, cancellationToken);

                        //Executing processing after waiting the specified time...
                        await ExecuteSqlTransactionalOutboxProcessingInternalAsync();
                        executionCount++;
                    }
                }
                catch (TaskCanceledException) 
                {
                    //Handle Cancellation Gracefully and do nothing...
                }

                return executionCount;
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public async Task<long> StopAsync()
        {
            long executionCount = 0;
            if (this.ProcessingTask != null)
            {
                //Signal all processing to stop...
                this.CancellationTokenSource.Cancel();

                //Await for processing to wrap up and complete the task!!!
                executionCount = await this.ProcessingTask.ConfigureAwait(false);
                
                //Cleanup the Processing Task!
                this.ProcessingTask.Dispose();
                this.ProcessingTask = null;
            }

            return executionCount;
        }

        protected virtual async Task ExecuteSqlTransactionalOutboxProcessingInternalAsync()
        {
            //************************************************************
            //*** Execute processing of the Transactional Outbox...
            //************************************************************
            await using var sqlConnection = new SqlConnection(this.SqlConnectionString);
            await sqlConnection.OpenAsync().ConfigureAwait(false);

            await sqlConnection
                .ProcessPendingOutboxItemsAsync(this.SqlTransactionalOutboxPublisher, this.SqlTransactionalOutboxProcessingOptions)
                .ConfigureAwait(false);

            //************************************************************
            //*** If specified then Execute Cleanup of Historical Outbox Data...
            //************************************************************
            if (this.HistoryToKeepTimespan > TimeSpan.Zero)
            {
                await sqlConnection
                    .CleanupHistoricalOutboxItemsAsync(this.HistoryToKeepTimespan)
                    .ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await this.StopAsync();
            
            //Finally cleanup other Disposable items (that WE Own)...
            //NOTE: We do NOT Dispose of items injected via Constructor as the ownership is not ours...
            this.CancellationTokenSource?.Dispose();
        }
    }
}
