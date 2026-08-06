using RazorRendererTests.Components;
using SpawnDev.SpawnJS.JSObjects;

namespace RazorRendererTests
{
    /// <summary>The interactive event round trip: a real DOM event drives a C# handler and the DOM updates.</summary>
    public class EventTests : RendererTestBase
    {
        /// <summary>Constructed by the runner.</summary>
        public EventTests(IServiceProvider services) : base(services) { }

        /// <summary>Clicking an <c>@onclick</c> button runs the handler, re-renders, and updates the DOM.</summary>
        [RendererTest]
        public async Task OnClickIncrementsCounterTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<Counter>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            Assert.Equal("0", TextOf(host, "[data-count]"), "counter should start at 0");

            using (var button = host.QuerySelector<HTMLElement>("[data-increment]"))
            {
                Assert.NotNull(button, "increment button not found");
                button!.Click();
            }

            var ok = await WaitForAsync(() => TextOf(host, "[data-count]") == "1");
            Assert.True(ok, $"counter did not reach 1 after click (saw '{TextOf(host, "[data-count]")}')");
        }

        /// <summary>
        /// A native click on a checkbox fires 'change', which the renderer turns into a ChangeEventArgs
        /// (checked bool) and <c>@bind</c> writes back into the field - the span reflecting it proves the
        /// two-way binding end to end.
        /// </summary>
        [RendererTest]
        public async Task BindCheckboxTwoWayTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<BindBox>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            Assert.Equal("off", TextOf(host, "[data-state]"), "bind state should start off");

            using (var toggle = host.QuerySelector<HTMLElement>("[data-toggle]"))
            {
                Assert.NotNull(toggle, "checkbox not found");
                toggle!.Click();
            }

            var ok = await WaitForAsync(() => TextOf(host, "[data-state]") == "on");
            Assert.True(ok, $"@bind did not propagate the checkbox toggle (saw '{TextOf(host, "[data-state]")}')");
        }

        /// <summary>
        /// Typing into a text input - set its value, dispatch a real 'input' Event - drives
        /// <c>@bind:event="oninput"</c>, which reads HTMLInputElement.Value and binds it back into the field.
        /// </summary>
        [RendererTest]
        public async Task BindTextInputOnInputTest()
        {
            HostCapabilities.RequireBrowser();
            var host = NewHost();
            var mappings = NewMappings();
            mappings.Add<TextBindBox>(host);
            var r = NewRenderer(mappings);
            await r.Ready;

            Assert.Equal("", TextOf(host, "[data-echo]"), "text bind should start empty");

            using (var input = host.QuerySelector<HTMLInputElement>("[data-text]"))
            {
                Assert.NotNull(input, "text input not found");
                input!.Value = "hello";
                using var ev = new Event("input");
                input.DispatchEvent(ev);
            }

            var ok = await WaitForAsync(() => TextOf(host, "[data-echo]") == "hello");
            Assert.True(ok, $"@bind oninput did not propagate typed text (saw '{TextOf(host, "[data-echo]")}')");
        }
    }
}
