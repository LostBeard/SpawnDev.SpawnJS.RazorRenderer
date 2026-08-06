namespace RazorRendererTests
{
    /// <summary>
    /// Minimal assertion helpers. A failed assertion throws, which the runner reports as a failed test with
    /// the message attached - so every message must say what was expected and what was seen.
    /// </summary>
    public static class Assert
    {
        /// <summary>Fails unless <paramref name="condition"/> is true.</summary>
        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        /// <summary>Fails unless <paramref name="condition"/> is false.</summary>
        public static void False(bool condition, string message) => True(!condition, message);

        /// <summary>Fails unless the two strings are ordinally equal.</summary>
        public static void Equal(string? expected, string? actual, string message)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception($"{message} (expected '{expected}', actual '{actual}')");
        }

        /// <summary>Fails unless the two ints are equal.</summary>
        public static void Equal(int expected, int actual, string message)
        {
            if (expected != actual)
                throw new Exception($"{message} (expected {expected}, actual {actual})");
        }

        /// <summary>Fails when <paramref name="value"/> is null.</summary>
        public static void NotNull(object? value, string message)
        {
            if (value is null) throw new Exception(message);
        }

        /// <summary>Fails when <paramref name="value"/> is not null.</summary>
        public static void Null(object? value, string message)
        {
            if (value is not null) throw new Exception(message);
        }
    }
}
