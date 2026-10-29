using System.Globalization;
using System.Runtime.CompilerServices;

namespace Stikling.Core.Tests;

/// <summary>
/// Runs the tests with English text, so tests that check text from the resource files in Core pass on a
/// computer set to another language too.
/// </summary>
internal static class EnglishText
{
    [ModuleInitializer]
    internal static void Use()
    {
        var english = CultureInfo.GetCultureInfo("en-GB");
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentUICulture = english;
    }
}
