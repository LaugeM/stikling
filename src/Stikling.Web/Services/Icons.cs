using Microsoft.AspNetCore.Components;

namespace Stikling.Web.Services;

/// <summary>The drawings used on more than one screen, as the inside of a 24 by 24 stroked svg. The <c>&lt;svg&gt;</c> wrapper stays in the page, so scoped styles for <c>svg</c> still match.</summary>
public static class Icons
{
    public static readonly MarkupString ArrowToEnd = new(
        """<path d="M4 12h12M12 7l5 5-5 5" /><path d="M20 5v14" />""");

    public static readonly MarkupString ArrowToStart = new(
        """<path d="M20 12H8M12 7l-5 5 5 5" /><path d="M4 5v14" />""");

    public static readonly MarkupString Book = new(
        """<path d="M12 6.500C10.500 5 8 4.500 4.500 4.500v13c3.500 0 6 .5 7.500 2 1.500-1.500 4-2 7.500-2v-13c-3.500 0-6 .5-7.500 2Z" /><path d="M12 6.500v13" />""");

    public static readonly MarkupString Camera = new(
        """<path d="M4 8h3l2-3h6l2 3h3v11H4Z" /><circle cx="12" cy="13" r="3.5" />""");

    public static readonly MarkupString Check = new(
        """<path d="m5 12.5 4.5 4.5L19 7.5" />""");

    public static readonly MarkupString CheckBox = new(
        """<rect x="4" y="4" width="16" height="16" rx="3" /><path d="m8.5 12.2 2.4 2.4 4.6-5" />""");

    public static readonly MarkupString ChevronDown = new(
        """<path d="m6 9 6 6 6-6" />""");

    public static readonly MarkupString ChevronLeft = new(
        """<path d="m15 5-7 7 7 7" />""");

    public static readonly MarkupString ChevronRight = new(
        """<path d="m9 5 7 7-7 7" />""");

    public static readonly MarkupString ChevronRightSmall = new(
        """<path d="m9 6 6 6-6 6" />""");

    public static readonly MarkupString Clock = new(
        """<circle cx="12" cy="12" r="8.5" /><path d="M12 7.5V12l3 2" />""");

    public static readonly MarkupString Close = new(
        """<path d="M6 6l12 12M18 6 6 18" />""");

    public static readonly MarkupString Cloud = new(
        """<path d="M7 18a4 4 0 0 1-.7-7.9A5.5 5.5 0 0 1 17 9.5 4.3 4.3 0 0 1 17 18Z" />""");

    public static readonly MarkupString Document = new(
        """<path d="M6 3h9l4 4v14H6Z" /><path d="M9 11h7M9 15h7M9 7h3" />""");

    public static readonly MarkupString Drop = new(
        """<path d="M12 3.5c3 4 5.5 7 5.5 10a5.5 5.5 0 0 1-11 0c0-3 2.5-6 5.5-10Z" />""");

    public static readonly MarkupString EmptyPhoto = new(
        """<rect x="2.5" y="3.5" width="19" height="17" rx="3" stroke-dasharray="2.5 3" opacity="0.7" /><path d="M7 10.5h2l1-1.5h4l1 1.5h2V16H7Z" /><circle cx="12" cy="13" r="1.7" />""");

    public static readonly MarkupString Eye = new(
        """<path d="M2.5 12S6 5.5 12 5.5 21.5 12 21.5 12 18 18.5 12 18.5 2.5 12 2.5 12Z" /><circle cx="12" cy="12" r="3" />""");

    public static readonly MarkupString Feed = new(
        """<path d="M8 3h8l-1 6v9a3 3 0 0 1-6 0V9Z" /><path d="M12 12v4M12 16l-2 2M12 16l2 2" />""");

    public static readonly MarkupString Flag = new(
        """<path d="M5 21V4" /><path d="M5 4h12l-2.5 4.5L17 13H5" />""");

    public static readonly MarkupString Moon = new(
        """<path d="M20 14.5A8 8 0 1 1 9.5 4a6.5 6.5 0 0 0 10.5 10.5Z" />""");

    public static readonly MarkupString More = new(
        """<circle cx="5" cy="12" r="1.3" /><circle cx="12" cy="12" r="1.3" /><circle cx="19" cy="12" r="1.3" />""");

    public static readonly MarkupString Pencil = new(
        """<path d="M4 20h4L19.5 8.5a2.1 2.1 0 0 0-3-3L5 17Z" />""");

    public static readonly MarkupString Pest = new(
        """<circle cx="12" cy="13" r="4" /><path d="M12 9V6M8.5 10.5 6 8M15.5 10.5 18 8M8 13H4M20 13h-4M8.5 15.5 6 18M15.5 15.5 18 18M12 17v3" />""");

    public static readonly MarkupString Photo = new(
        """<rect x="3.5" y="4.5" width="17" height="15" rx="2" /><circle cx="9" cy="10" r="1.8" /><path d="m4 17 5-5 4 4 3-3 4 4" />""");

    public static readonly MarkupString Plus = new(
        """<path d="M12 5v14M5 12h14" />""");

    public static readonly MarkupString Pot = new(
        """<path d="M4.5 4.5h15v3.5h-15Z" /><path d="M6 8l1.5 11a1.5 1.5 0 0 0 1.5 1.3h6a1.5 1.5 0 0 0 1.5-1.3L18 8" />""");

    public static readonly MarkupString Repeat = new(
        """<path d="M7 7h11l-3-3M17 17H6l3 3" />""");

    public static readonly MarkupString Scissors = new(
        """<circle cx="6" cy="6.5" r="2.5" /><circle cx="6" cy="17.5" r="2.5" /><path d="M8.2 7.8 19 17M8.2 16.2 19 7" />""");

    public static readonly MarkupString Search = new(
        """<circle cx="11" cy="11" r="7" /><path d="M20 20l-4-4" />""");

    public static readonly MarkupString Seedling = new(
        """<path d="M12 21v-9" /><path d="M12 12C6 12 4 8 4 4c6 0 8 3.5 8 8Z" /><path d="M12 15c5 0 8-3 8-7-5 0-8 3-8 7Z" />""");

    public static readonly MarkupString Settings = new(
        """<circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z" />""");

    public static readonly MarkupString Sprig = new(
        """<path d="M12 21v-7" /><path d="M12 14c-1-4-4-6-8-6 0 4 3 7 8 6Z" /><path d="M12 14c2-3 4.5-4 8-3.5-.5 3.5-3.5 5-8 3.5Z" /><path d="M4 21h16" />""");

    public static readonly MarkupString Sprout = new(
        """<path d="M12 21v-9" /><path d="M12 12C12 7 8 4 4 4c0 4 3 8 8 8Z" /><path d="M12 14c0-4 3-7 8-7 0 4-3 7-8 7Z" />""");

    public static readonly MarkupString Star = new(
        """<path d="M12 2.5l2.9 6.1 6.6.8-4.9 4.6 1.3 6.6L12 17.3 6.1 20.6l1.3-6.6L2.5 9.4l6.6-.8Z" />""");

    public static readonly MarkupString Sun = new(
        """<circle cx="12" cy="12" r="4" /><path d="M12 3v2M12 19v2M3 12h2M19 12h2M5.6 5.6 7 7M17 17l1.4 1.4M5.6 18.4 7 17M17 7l1.4-1.4" />""");

    public static readonly MarkupString Swap = new(
        """<path d="M4 9h14l-3.5-3.5" /><path d="M20 15H6l3.5 3.5" />""");

    public static readonly MarkupString Tag = new(
        """<path d="M3 12V5a2 2 0 0 1 2-2h7l9 9-9 9Z" /><circle cx="8" cy="8" r="1.4" />""");

    public static readonly MarkupString Tick = new(
        """<path d="M6 12.5l4 4 8-9" />""");
}
