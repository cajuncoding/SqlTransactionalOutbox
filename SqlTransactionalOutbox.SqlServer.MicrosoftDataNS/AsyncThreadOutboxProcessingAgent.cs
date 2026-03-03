#nullable disable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlAppLockHelper;
using SqlAppLockHelper.MicrosoftDataNS;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox.SqlServer.MicrosoftDataNS
{
    public class AsyncThreadOutboxProcessingAgent : IAsyncDisposable
    {
        public static string DefaultDistributedLockName { get; } = $"SqlTransactionalOutbox.{nameof(AsyncThreadOutboxProcessingAgent)}.DefaultDistributedLock";
        
        protected string SqlConnectionString { get; }

        protected ISqlTransactionalOutboxPublisher<Guid> SqlTransactionalOutboxPublisher { get; }
        protected OutboxProcessingOptions SqlTransactionalOutboxProcessingOptions { get; }
        
        protected bool IsDistributedLockingEnabled { get; set; } = true;
        protected string DistributedLockName { get; set; } = DefaultDistributedLockName;

        protected TimeSpan ProcessingIntervalTimespan { get; }
        protected TimeSpan HistoryToKeepTimespan { get; }

        protected CancellationTokenSource CancellationTokenSource { get; set; }

        protected Task ProcessingTask { get; set; }

        long _executionCount = 0;
        public long ExecutionCount => _executionCount;

        public AsyncThreadOutboxProcessingAgent(
            TimeSpan processingIntervalTimeSpan,
            string sqlConnectionString,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            OutboxProcessingOptions outboxProcessingOptions = null,
            bool enableDistributedLockForProcessing = true,
            string distributedLockNameOverride = null
        ) : this(processingIntervalTimeSpan, TimeSpan.Zero, sqlConnectionString, outboxPublisher, outboxProcessingOptions, enableDistributedLockForProcessing, distributedLockNameOverride)
        { }

        public AsyncThreadOutboxProcessingAgent(
            TimeSpan processingIntervalTimeSpan,
            TimeSpan historyToKeepTimeSpan,
            string sqlConnectionString,
            ISqlTransactionalOutboxPublisher<Guid> outboxPublisher,
            OutboxProcessingOptions outboxProcessingOptions = null,
            bool enableDistributedLockForProcessing = true,
            string distributedLockNameOverride = null
        )
        {
            this.ProcessingIntervalTimespan = processingIntervalTimeSpan.AssertNotNull(nameof(processingIntervalTimeSpan));
            this.HistoryToKeepTimespan = historyToKeepTimeSpan.AssertNotNull(nameof(historyToKeepTimeSpan));
            this.SqlConnectionString = sqlConnectionString.AssertNotNullOrWhiteSpace(nameof(sqlConnectionString));
            this.SqlTransactionalOutboxPublisher = outboxPublisher.AssertNotNull(nameof(outboxPublisher));
            this.SqlTransactionalOutboxProcessingOptions = outboxProcessingOptions ?? OutboxProcessingOptions.DefaultOutboxProcessingOptions;
            this.IsDistributedLockingEnabled = enableDistributedLockForProcessing;
            if(!string.IsNullOrWhiteSpace(distributedLockNameOverride))
                this.DistributedLockName = distributedLockNameOverride;
        }

        public Task StartAsync()
        {
            this.CancellationTokenSource = new CancellationTokenSource();
            
            var cancellationToken = this.CancellationTokenSource.Token;
            Interlocked.Exchange(ref _executionCount, 0);

            this.ProcessingTask = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        //Wait for the next iteration to Process the Outbox...
                        await Task.Delay(this.ProcessingIntervalTimespan, cancellationToken);

                        //Executing processing after waiting the specified time...
                        await ExecuteSqlTransactionalOutboxProcessingInternalAsync();
                        Interlocked.Increment(ref _executionCount);
                    }
                }
                catch (TaskCanceledException)
                {
                    //Handle Cancellation Gracefully and do nothing...
                }
            }, cancellationToken);

            //We always return a CompletedTask so that calling code can continue...
            //NOTE: If we returned the ProcessingTask then calling code would be blocked!
            return Task.CompletedTask;
        }

        public async Task<long> StopAsync()
        {
            if (this.ProcessingTask != null)
            {
                //Signal all processing to stop...
                this.CancellationTokenSource.Cancel();

                //Await for processing to wrap up and complete the task!!!
                await this.ProcessingTask.ConfigureAwait(false);
                
                //Cleanup the Processing Task!
                this.ProcessingTask.Dispose();
                this.ProcessingTask = null;
            }

            return this.ExecutionCount;
        }

        protected virtual async Task ExecuteSqlTransactionalOutboxProcessingInternalAsync()
        {
            //************************************************************
            //*** Execute processing of the Transactional Outbox...
            //************************************************************
            await using var sqlConnection = new SqlConnection(this.SqlConnectionString);
            await sqlConnection.OpenAsync().ConfigureAwait(false);

            //If enabled (default) then attempt to acquire the Distributed Lock for processing to
            //  ensure only one processor is processing the Outbox at a given time across distributed instances...
            //NOTE: We disable the exception throwing feature so that we can safely & passively evaluate if the distributed
            //      lock was acquired or not and simply skip processing if we fail to acquire the lock (because another instance is processing)
            //      As opposed to allowing the throwing an exception which would be disruptive to the processing agent and require additional handling/logging.
            SqlServerAppLock sqlDistributedLock = IsDistributedLockingEnabled
                ? await sqlConnection.AcquireAppLockAsync(DistributedLockName, throwsException: false)
                : null;

            if (!IsDistributedLockingEnabled || sqlDistributedLock.IsLockAcquired)
            {
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

            //WE MUST Dispose of the Lock to enure it is released so other instances may be able to acquire it . . . 
            if(sqlDistributedLock != null)
                await sqlDistributedLock.DisposeAsync().ConfigureAwait(false);
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
