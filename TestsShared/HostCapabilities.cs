using SpawnDev.SpawnJS;

namespace RazorRendererTests
{
    /// <summary>
    /// Host capability checks. These tests drive the real DOM through a live renderer, so they require a
    /// browser host. On a non-browser host they skip rather than fail - a red suite there would hide real
    /// regressions behind an expected absence.
    /// </summary>
    public static class HostCapabilities
    {
        static SpawnJSRuntime JS => SpawnJSRuntime.Instance ?? throw new InvalidOperationException("SpawnJSRuntime has not been created.");

        /// <summary>True when running in a browser (a <c>window</c> global exists).</summary>
        public static bool IsBrowser => JS.Has("window");

        /// <summary>Skips the calling test unless it is running in a browser.</summary>
        public static void RequireBrowser()
        {
            if (!IsBrowser) throw new SkipTestException("requires a browser host (no window global)");
        }
    }
}
