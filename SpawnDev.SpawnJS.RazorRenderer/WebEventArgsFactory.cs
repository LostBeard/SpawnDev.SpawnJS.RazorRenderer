using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.JSObjects;
using ErrorEventArgs = Microsoft.AspNetCore.Components.Web.ErrorEventArgs;

namespace SpawnDev.SpawnJS.RazorRenderer;

/// <summary>
/// Builds the strongly-typed Blazor <see cref="EventArgs"/> for a DOM event, reading the event through
/// SpawnJS's typed wrappers (<see cref="MouseEvent"/>, <see cref="KeyboardEvent"/>, ...) - never raw JSRef.
/// <para>
/// The handler's parameter type must be assignable from what we pass, so an <c>@onclick</c> that takes
/// <see cref="MouseEventArgs"/> needs a real <see cref="MouseEventArgs"/>, not <see cref="EventArgs.Empty"/>.
/// We therefore key the concrete args type off the DOM event name, with the same event-name table as Blazor's own
/// (Microsoft.AspNetCore.Components.Web EventTypes). An event family missing here reached a typed handler as
/// EventArgs.Empty and Blazor's cast to it failed - every <c>(PointerEventArgs e) =&gt;</c> handler silently never ran
/// (found 2026-10-06, Anaglyphohol's toolbar drag).
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
                {
                    using var m = ev.JSRefAs<MouseEvent>();
                    return FillMouse(new MouseEventArgs(), eventName, m);
                }

            case "pointerdown":
            case "pointerup":
            case "pointermove":
            case "pointerover":
            case "pointerout":
            case "pointerenter":
            case "pointerleave":
            case "pointercancel":
            case "gotpointercapture":
            case "lostpointercapture":
                return BuildPointer(eventName, ev);

            case "wheel":
            case "mousewheel":
                return BuildWheel(eventName, ev);

            case "touchstart":
            case "touchend":
            case "touchmove":
            case "touchcancel":
            case "touchenter":
            case "touchleave":
                return BuildTouch(eventName, ev);

            case "drag":
            case "dragend":
            case "dragenter":
            case "dragleave":
            case "dragover":
            case "dragstart":
            case "drop":
                return BuildDrag(eventName, ev);

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

            case "copy":
            case "cut":
            case "paste":
                return new ClipboardEventArgs { Type = eventName };

            case "loadstart":
            case "timeout":
            case "abort":
            case "load":
            case "loadend":
            case "progress":
                return BuildProgress(eventName, ev);

            case "error":
                return BuildError(eventName, ev);

            default:
                return EventArgs.Empty;
        }
    }

    /// <summary>The MouseEvent members, shared by every args type that derives from MouseEventArgs.</summary>
    static T FillMouse<T>(T args, string eventName, MouseEvent m) where T : MouseEventArgs
    {
        args.Type = eventName;
        args.Detail = m.Detail;
        args.ClientX = m.ClientX;
        args.ClientY = m.ClientY;
        args.ScreenX = m.ScreenX;
        args.ScreenY = m.ScreenY;
        args.OffsetX = m.OffsetX;
        args.OffsetY = m.OffsetY;
        args.PageX = m.PageX;
        args.PageY = m.PageY;
        args.MovementX = m.MovementX;
        args.MovementY = m.MovementY;
        args.Button = (long)m.Button;
        args.Buttons = (long)m.Buttons;
        args.CtrlKey = m.CtrlKey;
        args.ShiftKey = m.ShiftKey;
        args.AltKey = m.AltKey;
        args.MetaKey = m.MetaKey;
        return args;
    }

    static PointerEventArgs BuildPointer(string eventName, Event ev)
    {
        using var p = ev.JSRefAs<PointerEvent>();
        var args = FillMouse(new PointerEventArgs(), eventName, p);
        args.PointerId = p.PointerId;
        args.Width = (float)p.Width;
        args.Height = (float)p.Height;
        args.Pressure = (float)p.Pressure;
        args.TiltX = (float)p.TiltX;
        args.TiltY = (float)p.TiltY;
        args.PointerType = p.PointerType;
        args.IsPrimary = p.IsPrimary;
        return args;
    }

    static WheelEventArgs BuildWheel(string eventName, Event ev)
    {
        using var w = ev.JSRefAs<WheelEvent>();
        var args = FillMouse(new WheelEventArgs(), eventName, w);
        args.DeltaX = w.DeltaX;
        args.DeltaY = w.DeltaY;
        args.DeltaZ = w.DeltaZ;
        args.DeltaMode = (long)w.DeltaMode;
        return args;
    }

    static TouchEventArgs BuildTouch(string eventName, Event ev)
    {
        using var t = ev.JSRefAs<TouchEvent>();
        using var touches = t.Touches;
        using var targetTouches = t.TargetTouches;
        using var changedTouches = t.ChangedTouches;
        return new TouchEventArgs
        {
            Type = eventName,
            Detail = t.Detail,
            Touches = TouchPoints(touches),
            TargetTouches = TouchPoints(targetTouches),
            ChangedTouches = TouchPoints(changedTouches),
            CtrlKey = t.CtrlKey,
            ShiftKey = t.ShiftKey,
            AltKey = t.AltKey,
            MetaKey = t.MetaKey,
        };
    }

    static TouchPoint[] TouchPoints(TouchList? list)
    {
        if (list == null) return System.Array.Empty<TouchPoint>();
        var points = new TouchPoint[list.Length];
        for (int i = 0; i < points.Length; i++)
        {
            using var touch = list.Items(i);
            points[i] = new TouchPoint
            {
                Identifier = touch.Identifier,
                ScreenX = touch.ScreenX,
                ScreenY = touch.ScreenY,
                ClientX = touch.ClientX,
                ClientY = touch.ClientY,
                PageX = touch.PageX,
                PageY = touch.PageY,
            };
        }
        return points;
    }

    static DragEventArgs BuildDrag(string eventName, Event ev)
    {
        using var d = ev.JSRefAs<DragEvent>();
        var args = FillMouse(new DragEventArgs(), eventName, d);
        // a script-made DragEvent may carry no dataTransfer: an empty one, as Blazor's own args never hold null there
        using SpawnDev.SpawnJS.JSObjects.DataTransfer? dt = d.DataTransfer;
        args.DataTransfer = dt == null ? new Microsoft.AspNetCore.Components.Web.DataTransfer() : BuildDataTransfer(dt);
        return args;
    }

    static Microsoft.AspNetCore.Components.Web.DataTransfer BuildDataTransfer(SpawnDev.SpawnJS.JSObjects.DataTransfer dt)
    {
        string[] files;
        using (var fileList = dt.Files)
        {
            files = new string[fileList?.Length ?? 0];
            for (int i = 0; i < files.Length; i++)
            {
                using var file = fileList!.Item(i);
                files[i] = file.Name;
            }
        }
        Microsoft.AspNetCore.Components.Web.DataTransferItem[] items;
        using (var itemList = dt.Items)
        {
            items = new Microsoft.AspNetCore.Components.Web.DataTransferItem[itemList?.Length ?? 0];
            for (int i = 0; i < items.Length; i++)
            {
                using var item = itemList!.Item(i);
                items[i] = new Microsoft.AspNetCore.Components.Web.DataTransferItem { Kind = item.Kind, Type = item.Type };
            }
        }
        return new Microsoft.AspNetCore.Components.Web.DataTransfer
        {
            DropEffect = dt.DropEffect,
            EffectAllowed = dt.EffectAllowed,
            Files = files,
            Items = items,
            Types = dt.Types ?? System.Array.Empty<string>(),
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

    // 'load' / 'error' on an <img> or <script> are plain Events, not ProgressEvent / ErrorEvent: the members those
    // carry read as absent (0 / null), as in Blazor.
    static ProgressEventArgs BuildProgress(string eventName, Event ev)
    {
        using var p = ev.JSRefAs<ProgressEvent>();
        return new ProgressEventArgs
        {
            Type = eventName,
            LengthComputable = p.LengthComputable ?? false,
            Loaded = (long)(p.Loaded ?? 0),
            Total = (long)(p.Total ?? 0),
        };
    }

    static ErrorEventArgs BuildError(string eventName, Event ev)
    {
        using var e = ev.JSRefAs<ErrorEvent>();
        bool isErrorEvent = e.JSRef!.Has("lineno");   // on ErrorEvent.prototype; a plain Event has none
        return new ErrorEventArgs
        {
            Type = eventName,
            Message = isErrorEvent ? e.Message : null,
            Filename = isErrorEvent ? e.Filename : null,
            Lineno = isErrorEvent ? e.LineNO : 0,
            Colno = isErrorEvent ? e.ColNO : 0,
        };
    }
}
