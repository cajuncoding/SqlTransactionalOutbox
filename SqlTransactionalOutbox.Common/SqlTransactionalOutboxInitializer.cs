using System;
using System.Text.Json;
using SqlTransactionalOutbox.CustomExtensions;
using SystemTextJsonHelpers;

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

        internal static JsonSerializerOptions InternalSqlTransactionalOutboxDefaultJsonSerializerOptions { get; set; }

        public static JsonSerializerOptions DefaultJsonSerializerOptions 
            => InternalSqlTransactionalOutboxDefaultJsonSerializerOptions ?? SystemTextJsonDefaults.DefaultSerializerOptions;
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
                        lockAcquisitionTimeoutSeconds: 0
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

            /// <summary>
            /// Initialize the global default settings for the OutboxTableConfig which will be supported by all convenience methods (e.g. Sql Custom Extensions)!
            /// NOTE: This should generally ONLY be called once at application startup and any thread concerns must be manually controlled by the calling code!
            /// </summary>
            /// <param name="customConfig"></param>
            public ConfigBuilder WithDefaultJsonSerializerOptions(JsonSerializerOptions jsonSerializerOptions)
            {
                SqlTransactionalOutboxDefaults.InternalSqlTransactionalOutboxDefaultJsonSerializerOptions = jsonSerializerOptions;
                return this;
            }

            /// <summary>
            /// Initialize SystemTextJsonHelpers.SystemTextJsonDefaults.DefaultSerializerOptions as the global default System.Text.Json serializer options for JSON Serialization
            /// that is automaticaly handled by the SqlTransactionalOutbox library  which will be used by all convenience methods (e.g. Sql Custom Extensions)!
            /// NOTE: THis is the default behavior if no other options are specified but this method allows for re-setting/enforcing the use of the SystemTextJsonHelpers defaults.
            /// NOTE: This should generally ONLY be called once at application startup and any thread concerns must be manually controlled by the calling code!
            /// </summary>
            /// <param name="customConfig"></param>
            public ConfigBuilder UseSystemTextJsonDefaultsFromSystemTextJsonHelpers()
            {
                SqlTransactionalOutboxDefaults.InternalSqlTransactionalOutboxDefaultJsonSerializerOptions = null;
                return this;
            }

            /// <summary>
            /// Initialize the global default options/settings used when processing the outbox items which will be supported by all convenience methods (e.g. Sql Custom Extensions).
            /// </summary>
            /// <param name="configureOptionsAction"></param>
            /// <returns></returns>
            public ConfigBuilder ConfigureOutboxProcessingOptions(Action<OutboxProcessingOptions> configureOptionsAction)
            {
                OutboxProcessingOptions.ConfigureDefaults(configureOptionsAction);
                return this;
            }
        }
    }
}
