using System;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox
{
    public static class SqlTransactionalOutboxDefaults
    {
        public static OutboxTableConfig OutboxTableConfig { get; internal set; } = new OutboxTableConfig();
        public static int DistributedMutexAcquisitionTimeoutSeconds { get; internal set; } = 1;
        public static string DistributeMutexLockPrefix { get; internal set; } = "SqlServerTransactionalOutboxProcessor::";
    }

    public class SqlTransactionalOutboxInitializer
    {
        /// <summary>
        /// Initialize the global default settings for the OutboxTableConfig which will be supported by all convenience methods (e.g. Sql Custom Extensions)!
        /// NOTE: This should ONLY be called once at application startup and any thread concerns must be manually controlled by the calling code!
        /// </summary>
        /// <param name="customConfig"></param>
        public static void SetOutboxTableConfig(OutboxTableConfig customConfig)
        {
            SqlTransactionalOutboxDefaults.OutboxTableConfig = customConfig.AssertNotNull(nameof(customConfig));
        }

        /// <summary>
        /// Initialize the global default settings for the Distributed Mutex settings which will be supported by all convenience methods (e.g. Sql Custom Extensions)!
        /// NOTE: This should ONLY be called once at application startup and any thread concerns must be manually controlled by the calling code!
        /// </summary>
        /// <param name="distributedMutexAcquisitionTimeoutSeconds"></param>
        /// <param name="distributedMutexLockPrefix"></param>
        public void SetDistributedMutexLockConfig(
            int? distributedMutexAcquisitionTimeoutSeconds = null,
            string distributedMutexLockPrefix = null
        )
        {
            if(distributedMutexAcquisitionTimeoutSeconds >= 0)
                SqlTransactionalOutboxDefaults.DistributedMutexAcquisitionTimeoutSeconds = (int)distributedMutexAcquisitionTimeoutSeconds;

            if (!string.IsNullOrWhiteSpace(distributedMutexLockPrefix))
                SqlTransactionalOutboxDefaults.DistributeMutexLockPrefix = distributedMutexLockPrefix;
        }

    }
}
