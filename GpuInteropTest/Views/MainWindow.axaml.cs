using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GpuInteropTest.Services;
using GpuInteropTest.ViewModels;

namespace GpuInteropTest.Views;

public partial class MainWindow : Window, IOpenGLContextService
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(this);
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel) DataContext!;

    private void Label_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Console.WriteLine("Attached");
    }

    public PixelSize ViewportSize
    {
        get => GlControl.WindowSize;
        set => GlControl.WindowSize = value;
    }
    public bool VSync
    {
        get => GlControl.VSync;
        set => GlControl.VSync = value;
    }
    public void MakeCurrent()
    {
        GlControl.MakeContextCurrent();
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            GlControl.InitRenderLoop();
        });
    }

    public IntPtr GetProcAddress(string sym)
    {
        return GlControl.GetProcAddress(sym);
    }

    public void SwapBuffers()
    {
        GlControl.SwapBuffers();
        Thread.Sleep(50);
    }
}