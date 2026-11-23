using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Stikling.Core.Timeline;
using Stikling.Web.Services;

namespace Stikling.Web.Tests;

/// <summary>Runs the tests with English text, so they pass on a computer set to another language too.</summary>
internal static class EnglishText
{
    [ModuleInitializer]
    internal static void Use()
    {
        var english = CultureInfo.GetCultureInfo("en-GB");
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        CultureInfo.CurrentCulture = english;
    }
}

/// <summary>An implementation of any interface that answers every call with nothing: an empty list, null or a default.</summary>
public class EmptyRepository : DispatchProxy
{
    public static T Of<T>() where T : class => Create<T, EmptyRepository>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var type = targetMethod!.ReturnType;
        if (type == typeof(Task))
            return Task.CompletedTask;
        if (type == typeof(ValueTask))
            return default(ValueTask);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = type.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [EmptyOf(inner)]);
        }
        return EmptyOf(type);
    }

    private static object? EmptyOf(Type type)
    {
        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();
            if (arguments.Length == 1 && generic == typeof(List<>))
                return Activator.CreateInstance(type);
            if (arguments.Length == 1 && (generic == typeof(IReadOnlyList<>) || generic == typeof(IEnumerable<>) || generic == typeof(IReadOnlyCollection<>)))
                return Array.CreateInstance(arguments[0], 0);
            if (generic == typeof(IReadOnlyDictionary<,>) || generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments));
        }
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}

internal static class TestServices
{
    /// <summary>What the components that show a plant's tile need: the time and the covers, with no photos stored.</summary>
    public static void AddTileServices(this BunitContext context)
    {
        context.Services.AddSingleton(TimeProvider.System);
        context.Services.AddSingleton(EmptyRepository.Of<IPhotoRepository>());
        context.Services.AddScoped<CoverPhotos>();
    }
}
