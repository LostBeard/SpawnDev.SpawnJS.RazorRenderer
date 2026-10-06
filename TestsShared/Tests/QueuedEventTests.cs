using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>
    /// A DOM event's dispatch can queue behind work already waiting on the renderer's Dispatcher (an app's async
    /// loop leaves its continuations there). If that work re-renders and gives the element a new event handler id
    /// (any lambda that captures a loop variable or local does, every render), the id read when the event fired is
    /// gone by the time the dispatch runs: "There is no event handler associated with this event", click lost.
    /// Seen in a real app (MiniRover): a panel's first click was often dropped while its page re-rendered from a
    /// 20 Hz loop.
    /// </summary>
    public class QueuedEventTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public QueuedEventTests(IServiceProvider services) : base(services) { }

        /// <summary>Clicks delivered by the browser while re-renders keep replacing the handler all reach it.</summary>
        [RendererTest]
        public async Task ClicksDuringReRendersAreNotLostTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<QueuedClickBox>(host);
            var r = NewRenderer(mappings);
            await r.Ready;
            var box = QueuedClickBox.Current;
            Assert.NotNull(box, "component did not start");

            const int clicks = 20;
            using var button = host.QuerySelector<HTMLElement>("[data-queued-button]");
            Assert.NotNull(button, "button not found");
            // The browser clicks from its own tasks (setInterval), the way a real click arrives: not from inside .NET.
            using var clicker = new Function("el", "n",
                "return new Promise(done => { let i = 0; const t = setInterval(() => { el.click(); if (++i >= n) { clearInterval(t); done(i); } }, 1); });");
            var clicking = clicker.CallAsync<int>(null, button, clicks);

            // Meanwhile an async loop on the Dispatcher re-renders after every await, as an app's control loop does:
            // each continuation waits in the Dispatcher's queue, and a click arriving then queues behind it.
            var looping = r.Dispatcher.InvokeAsync(async () =>
            {
                for (int i = 0; i < 400 && !clicking.IsCompleted; i++)
                {
                    await Task.Yield();
                    box!.Rerender();
                }
            });
            int fired = await clicking;
            await looping;

            Assert.True(box!.Renders > clicks, $"only {box.Renders} renders happened, so the clicks did not race them");
            var ok = await WaitForAsync(() => TextOf(host, "[data-queued-clicks]") == fired.ToString());
            Assert.True(ok, $"{fired} clicks fired but {TextOf(host, "[data-queued-clicks]")} reached the component (the rest were lost)");
        }
    }
}
