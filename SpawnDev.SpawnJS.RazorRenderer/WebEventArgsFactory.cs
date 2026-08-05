using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// Builds the strongly-typed Blazor <see cref="EventArgs"/> for a DOM event, reading the event through
/// SpawnJS's typed wrappers (<see cref="MouseEvent"/>, <see cref="KeyboardEvent"/>, ...) - never raw JSRef.
/// <para>
/// The handler's parameter type must be assignable from what we pass, so an <c>@onclick</c> that takes
/// <see cref="MouseEventArgs"/> needs a real <see cref="MouseEventArgs"/>, not <see cref="EventArgs.Empty"/>.
/// We therefore key the concrete args type off the DOM event name.
/// </para>
/// </summary>
internal static class WebEventArgsFactory
{
    public static EventArgs Create(string eventName, Event ev)
    {
        switch (eventName)
        {
            case "click":
            case "dblclick":
            case "mousedown":
            case "mouseup":
            case "mousemove":
            case "mouseover":
            case "mouseout":
            case "mouseenter":
            case "mouseleave":
            case "contextmenu":
                return BuildMouse(eventName, ev);

            case "keydown":
            case "keyup":
            case "keypress":
                return BuildKeyboard(eventName, ev);

            case "change":
            case "input":
                return BuildChange(ev);

            case "focus":
            case "blur":
            case "focusin":
            case "focusout":
                return new FocusEventArgs { Type = eventName };

            default:
                return EventArgs.Empty;
        }
    }

    static MouseEventArgs BuildMouse(string eventName, Event ev)
    {
        using var m = ev.JSRefAs<MouseEvent>();
        return new MouseEventArgs
        {
            Type = eventName,
            ClientX = m.ClientX,
            ClientY = m.ClientY,
            ScreenX = m.ScreenX,
            ScreenY = m.ScreenY,
            OffsetX = m.OffsetX,
            OffsetY = m.OffsetY,
            PageX = m.PageX,
            PageY = m.PageY,
            Button = (long)m.Button,
            Buttons = (long)m.Buttons,
            CtrlKey = m.CtrlKey,
            ShiftKey = m.ShiftKey,
            AltKey = m.AltKey,
            MetaKey = m.MetaKey,
        };
    }

    static KeyboardEventArgs BuildKeyboard(string eventName, Event ev)
    {
        using var k = ev.JSRefAs<KeyboardEvent>();
        return new KeyboardEventArgs
        {
            Type = eventName,
            Key = k.Key,
            Code = k.Code,
            Location = (float)(int)k.Location,
            Repeat = k.Repeat,
            CtrlKey = k.CtrlKey,
            ShiftKey = k.ShiftKey,
            AltKey = k.AltKey,
            MetaKey = k.MetaKey,
        };
    }

    static ChangeEventArgs BuildChange(Event ev)
    {
        using var target = ev.Target;
        using var el = target.JSRefAs<HTMLElement>();
        var type = el.GetAttribute("type");
        using var input = target.JSRefAs<HTMLInputElement>();
        object? value = type is "checkbox" or "radio" ? input.Checked : input.Value;
        return new ChangeEventArgs { Value = value };
    }
}
