using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using SystemTextJsonHelpers;

namespace SqlTransactionalOutbox.SampleApp.Common.Configuration
{
    public static class LocalSettingsEnvironmentReader
    {
        /// <summary>
        /// BBernard
        /// Settings Adaptation to load local.settings.json file into Environment so that Azure Functions Tests can
        /// then be used directly with valid settings reading via Environment.GetEnvironmentVariable
        /// Original Source inspired by Stack Overflow answer here:
        ///     https://stackoverflow.com/a/50223191/7293142
        /// </summary>
        /// <exception cref="Exception"></exception>
        public static void SetupEnvironmentFromLocalSettingsJson()
        {
            const string settingsFileName = "local.settings.json";
            var basePath = Directory.GetCurrentDirectory();
            var localSettingsJsonText = File.ReadAllText(Path.Combine(basePath, settingsFileName));
            var localSettingsJson = localSettingsJsonText.FromJsonTo<JsonObject>();

            var valuesJson = localSettingsJson?["Values"] as JsonObject ?? throw new Exception($"'Values' node cannot be found in file [{settingsFileName}].");
            foreach (var setting in valuesJson.Where(kv => kv.Value is not null))
            {
                Environment.SetEnvironmentVariable(setting.Key, setting.Value!.GetValue<string>());
            }
        }
    }
}
