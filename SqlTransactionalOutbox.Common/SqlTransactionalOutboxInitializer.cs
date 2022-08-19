using System;
using System.Runtime.CompilerServices;
using SqlTransactionalOutbox.CustomExtensions;

namespace SqlTransactionalOutbox
{
    public static class SqlTransactionalOutboxDefaults
    {
        static SqlTransactionalOutboxDefaults()
        {
            //Initialize all Default Values!
            //NOTE: We use the Reset method for consistency while enabling re-use via Unit Tests, etc. so that the State can be reset at any time!
            SqlTransactionalOutboxInitializer.Configure(c => c.ResetToDefaults());
        }

        public static ISqlTransactionalOutboxTableConfig OutboxTableConfig { get; internal set; }
        public static int DistributedMutexAcquisitionTimeoutSeconds { get; internal set; }
        public static string DistributeMutexLockPrefix { get; internal set; }
    }

    public class SqlTransactionalOutboxInitializer
    {
        private static readonly object _padLock = new object();

        public static SqlTransactionalOutboxInitializer Configure(Action<ConfigBuilder> configAction)
        {
            lock (_padLock)
            {
                configAction?.Invoke(new ConfigBuilder());
            }

            // Does nothing currently but may in the future...
            return new SqlTransactionalOutboxInitializer();
        }

        public class ConfigBuilder
        {
            /// <summary>
            /// Prevent external construction to enforce our Configuration Syntax and protect future enhancements with less risk of breaking changes...
            /// </summary>
            internal ConfigBuilder()
            { }

            /// <summary>
            /// Initialize/Reset all configuration values to original Default values.
            /// </summary>
            /// <returns></returns>
            public ConfigBuilder ResetToDefaults()
            {
                return this
                    .WithOutboxTableConfig(new OutboxTableConfig())
                    .WithDistributedMutexLockSettings(
                        lockNamePrefix: "SqlServerTransactionalOutboxProcessor::",
                        lockAcquisitionTimeoutSeconds: 1
                    );
            }

            /// <summary>
            /// Initialize the global default settings for the OutboxTableConfig which will be supported by all convenience methods (e.g. Sql Custom Extensions)!
            /// NOTE: This should ONLY be called once at application startup and any thread concerns must be manually controlled by the calling code!
            /// </summary>
            /// <param name="customConfig"></param>
            public ConfigBuilder WithOutboxTableConfig(ISqlTransactionalOutboxTableConfig customConfig)
            {
                SqlTransactionalOutboxDefaults.OutboxTableConfig = customConfig.AssertNotNull(nameof(customConfig));
                return this;
            }

            /// <summary>
            /// Initialize the global default settings for the Distributed Mutex settings which will be supported by all convenience methods (e.g. Sql Custom Extensions)!
            /// NOTE: This should ONLY be called once at application startup and any thread concerns must be manually controlled by the calling code!
            /// </summary>
            /// <param name="lockAcquisitionTimeoutSeconds"></param>
            /// <param name="lockNamePrefix"></param>
            public ConfigBuilder WithDistributedMutexLockSettings(
                int? lockAcquisitionTimeoutSeconds = null,
                string lockNamePrefix = null
            )
            {
                if (lockAcquisitionTimeoutSeconds < 0)
                    throw new ArgumentOutOfRangeException(nameof(lockAcquisitionTimeoutSeconds), "Lock acquisition timeout must be 0 or greater.");
                else if (lockAcquisitionTimeoutSeconds.HasValue)
                    SqlTransactionalOutboxDefaults.DistributedMutexAcquisitionTimeoutSeconds = (int)lockAcquisitionTimeoutSeconds;

                //Though NOT Advised, for flexibility we Allow the client to set the Prefix to anything (event empty string) if they choose...
                if (lockNamePrefix != null)
                    SqlTransactionalOutboxDefaults.DistributeMutexLockPrefix = lockNamePrefix;

                return this;
            }
        }
    }
}
