using SpawnDev.SpawnJS;
using System.Diagnostics;
using System.Reflection;

namespace RazorRendererTests
{
    /// <summary>
    /// The RazorRenderer test suite runner.<br/>
    /// Discovers <see cref="RendererTestAttribute"/> methods, runs each on a fresh instance, and writes one
    /// machine readable line per test plus a summary line. Both a human watching the browser console and the
    /// Playwright TestRunner harness read the exact same output.
    /// </summary>
    public static class TestSuiteRunner
    {
        /// <summary>The test classes that make up the suite. Add new test classes here.</summary>
        public static Type[] TestTypes { get; } = new[]
        {
            typeof(RendererCoreTests),
            typeof(EventTests),
            typeof(RefTests),
            typeof(KeyedListTests),
            typeof(SharedStyleSheetTests),
            typeof(RazorUIThemeTests),
            typeof(UiProgressTests),
        };

        /// <summary>Milliseconds a test may run before it is reported as timed out. Overridable per test.</summary>
        public static int DefaultTimeoutMs { get; set; } = 30000;

        /// <summary>The filter the current run was started with, or null.</summary>
        public static string? CurrentFilter { get; private set; }

        /// <summary>
        /// Runs every test whose name contains <paramref name="filter"/> (null or empty runs all), building each
        /// instance with <paramref name="services"/>. Returns the number of failed tests.
        /// </summary>
        public static async Task<int> RunAllAsync(IServiceProvider services, string? filter = null)
        {
            CurrentFilter = filter;
            var passed = 0;
            var failed = 0;
            var skipped = 0;
            var tests = Discover(filter);
            Console.WriteLine($"READY: {tests.Count} tests" + (string.IsNullOrEmpty(filter) ? "" : $" (filter '{filter}')"));
            foreach (var (type, method) in tests)
            {
                var name = $"{type.Name}.{method.Name}";
                var attr = method.GetCustomAttribute<RendererTestAttribute>();
                var timeoutMs = attr?.Timeout > 0 ? attr.Timeout : DefaultTimeoutMs;
                var sw = Stopwatch.StartNew();
                string result;
                var detail = "";
                object? instance = null;
                try
                {
                    // one instance per test so a test can never inherit state from the one before it
                    instance = Activator.CreateInstance(type, new object[] { services });
                    var task = (Task)method.Invoke(instance, null)!;
                    var finished = await Task.WhenAny(task, Task.Delay(timeoutMs));
                    if (finished != task) throw new TimeoutException($"Test exceeded {timeoutMs}ms");
                    await task;
                    result = "Success";
                    passed++;
                }
                catch (Exception ex)
                {
                    while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                    if (ex is SkipTestException)
                    {
                        result = "Skipped";
                        detail = ex.Message;
                        skipped++;
                    }
                    else
                    {
                        result = "Error";
                        detail = $"{ex.GetType().Name}: {ex.Message}";
                        failed++;
                        SpawnJSRuntime.Instance?.LogError($"FAILED: {name}\n{ex}");
                    }
                }
                finally
                {
                    (instance as IDisposable)?.Dispose();
                }
                sw.Stop();
                // pipe delimited, single line, so the harness can parse it straight off the console
                Console.WriteLine($"TEST: {name}|{result}|{sw.ElapsedMilliseconds}|{Sanitize(detail)}");
            }
            Console.WriteLine($"RESULTS: Failed: {failed} Passed: {passed} Skipped: {skipped} Ran: {tests.Count}");
            return failed;
        }

        /// <summary>Returns every test method matching the filter, in declaration order per class.</summary>
        public static List<(Type Type, MethodInfo Method)> Discover(string? filter = null)
        {
            var found = new List<(Type, MethodInfo)>();
            foreach (var type in TestTypes)
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.GetCustomAttribute<RendererTestAttribute>() == null) continue;
                    if (method.ReturnType != typeof(Task) || method.GetParameters().Length != 0)
                        throw new Exception($"{type.Name}.{method.Name} is marked [RendererTest] but is not a no-argument method returning Task");
                    if (!string.IsNullOrEmpty(filter) && !$"{type.Name}.{method.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                    found.Add((type, method));
                }
            }
            return found;
        }

        /// <summary>Reads the <c>filter</c> query string value from the page url, if any.</summary>
        public static string? FilterFromLocation()
        {
            var JS = SpawnJSRuntime.Instance;
            if (JS == null) return null;
            var search = JS.Get<string?>("location.search");
            if (string.IsNullOrEmpty(search)) return null;
            foreach (var pair in search.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && parts[0] == "filter") return Uri.UnescapeDataString(parts[1]);
            }
            return null;
        }

        static string Sanitize(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
    }
}
