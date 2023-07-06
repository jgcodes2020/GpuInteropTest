using System;
using System.Threading.Tasks;
using Avalonia.Rendering.Composition;

namespace GpuInteropTest.Helpers;

public static class CompositionHelpers
{
    public static async Task<T> GetRenderInterfaceFeature<T>(this Compositor compositor)
    {
        var feature = await compositor.TryGetRenderInterfaceFeature(typeof(T));
        if (feature == null)
            throw new PlatformNotSupportedException($"Feature {typeof(T).FullName} not supported on this platform");
        return (T) feature;
    }
}