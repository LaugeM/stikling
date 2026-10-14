using Microsoft.AspNetCore.Components;
using Stikling.Core.Models;

namespace Stikling.Web.Services;

/// <summary>What the form for a problem is for.</summary>
public enum ProblemSheetMode
{
    /// <summary>What you see: a new problem, or changing one.</summary>
    Open,

    /// <summary>How it looks now and what was done: a new update, or correcting one.</summary>
    Update
}

/// <summary>The drawings for problems, as the inside of a 24 by 24 stroked svg.</summary>
public static class ProblemIcons
{
    /// <summary>A leaf on a stem that bends over, so the leaf hangs down.</summary>
    public static readonly MarkupString WiltingLeaf = new(
        """<path d="M8 21V11c0-3.500 2-6 6-6" /><path d="M14 5c3.500.5 5.500 3.500 4.500 8C15 12.500 13 9.500 14 5Z" />""");

    /// <summary>The arrow for a trend: up, flat or down.</summary>
    public static MarkupString Trend(ProblemTrend trend) => new(trend switch
    {
        ProblemTrend.Better => """<path d="M12 19V5" /><path d="m6 11 6-6 6 6" />""",
        ProblemTrend.Same => """<path d="M5 12h14" />""",
        _ => """<path d="M12 5v14" /><path d="m6 13 6 6 6-6" />"""
    });

    /// <summary>The class that gives a trend its colour.</summary>
    public static string TrendClass(ProblemTrend trend) => trend switch
    {
        ProblemTrend.Better => "trend-better",
        ProblemTrend.Same => "trend-same",
        _ => "trend-worse"
    };
}
