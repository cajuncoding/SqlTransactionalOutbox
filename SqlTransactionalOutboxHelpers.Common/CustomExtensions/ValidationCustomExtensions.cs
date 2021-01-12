using System;
using System.Collections.Generic;
using System.Text;

namespace SqlTransactionalOutboxHelpers.CustomExtensions
{
    public static class ValidationCustomExtensions
    {
        public static string AssertNotNullOrWhiteSpace(this string value, string argName, string errorMessage = null)
        {
            //Initialize User specified variables
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException(argName,
                    errorMessage ?? "A valid string must be specified; value cannot be null, empty, or whitespace."
                );

            return value;
        }

        public static T AssertNotNull<T>(this T value, string argName, string errorMessage = null) where T: class
        {
            //Initialize User specified variables
            if (value == null)
                throw new ArgumentNullException(argName,
                    errorMessage ?? "A valid non-null value must be specified."
                );

            return value;
        }
    }
}
