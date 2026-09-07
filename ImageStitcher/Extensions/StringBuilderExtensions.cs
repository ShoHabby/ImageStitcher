using System.Text;

namespace ImageStitcher.Extensions;

/// <summary>
/// StringBuilder extensions
/// </summary>
internal static class StringBuilderExtensions
{
    /// <param name="stringBuilder">StringBuilder instance</param>
    extension(StringBuilder stringBuilder)
    {
        /// <summary>
        /// Builds the string and then clears the StringBuilder
        /// </summary>
        /// <returns>The built string</returns>
        public string ToStringAndClear()
        {
            string value = stringBuilder.ToString();
            stringBuilder.Clear();
            return value;
        }
    }
}
