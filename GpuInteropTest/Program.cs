using Avalonia;
using Avalonia.ReactiveUI;
using System;

namespace GpuInteropTest;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    private static AppBuilder BuildAvaloniaApp()
    {
        var app = AppBuilder.Configure<App>()
            .LogToTrace()
            .UseReactiveUI()
            .UseSkia();
        if (OperatingSystem.IsWindows())
        {
            app = app.UseWin32();
            app.With(new Win32PlatformOptions
            {
                // Add Windows options here
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            app = app.UseX11();
            app.With(new X11PlatformOptions
            {
                EnableIme = true,
                WmClass = "io.github.mupen_rewrite.M64RPFW"
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            app = app.UseAvaloniaNative();
            app.With(new AvaloniaNativePlatformOptions
            {
                // Add MacOS options here
            });
        }

        return app;
    }
}