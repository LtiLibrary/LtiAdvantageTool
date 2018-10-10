using System;
using System.ComponentModel.DataAnnotations;

namespace AdvantageTool.Data
{
    /// <summary>
    /// Validates a URL.
    /// </summary>
    public class UrlAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            return value == null || Uri.TryCreate(Convert.ToString(value), UriKind.Absolute, out _);
        }
    }
}
