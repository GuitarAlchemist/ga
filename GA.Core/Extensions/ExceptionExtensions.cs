namespace GA.Core.Extensions;

public static class ExceptionExtensions
{
    /// <summary>
    /// Gets a string that contains the exception message, a new line, then the stack trace.
    /// </summary>
    /// <param name="ex">The <see cref="Exception"/>.</param>
    /// <param name="header">The header.</param>
    /// <param name="details">When set to true, messages from inner exceptions are included.</param>
    /// <returns>A <see cref="string"/>.</returns>
    public static string GetMessageAndStackTrace(
        this Exception ex,
        string header,
        bool details = false)
    {
        string result;

        if (ex is AggregateException aggregateException)
        {
            var sb = new StringBuilder($"{header}:{Environment.NewLine}");
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine("====================================================");
                }
                sb.AppendLine(innerException.Message);
                sb.AppendLine(innerException.StackTrace);
            }
            result = sb.ToString();
        }
        else
        {
            var sb = new StringBuilder($"{header}: {ex.Message}");
            if (details)
            {
                var level = 1;
                var currentLevel = ex;
                while (true)
                {
                    currentLevel = currentLevel.InnerException;
                    if (currentLevel == null) break;
                    sb.AppendLine($"[Inner exception level {level++}] {currentLevel.Message}");
                }
            }

            sb.AppendLine(ex.StackTrace);
            result = sb.ToString();
        }

        return result;
    }

    public static Exception? GetInnerException(this Exception? ex)
    {
        while (true)
        {
            Exception? innerException = null;
            if (ex is AggregateException aggregateException)
            {
                innerException = aggregateException.InnerException;
            }

            if (innerException == null)
            {
                break;
            }

            ex = innerException;
        }

        return ex;
    }
}