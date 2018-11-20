using System;
using System.ComponentModel.DataAnnotations;

namespace AdvantageTool.Utility
{
    /// <inheritdoc />
    /// <summary>
    /// Validates a URL. Accepts localhost.
    /// </summary>
    public class LocalhostUrlAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            return value == null || Uri.TryCreate(Convert.ToString(value), UriKind.Absolute, out _);
        }
    }
}
