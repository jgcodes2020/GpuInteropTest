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

    private IDisposable _contextDispose;
    
    public void MakeCurrent()
    {
        _contextDispose = GlControl.MakeContextCurrent()!;
    }

    public IntPtr GetProcAddress(string sym)
    {
        return GlControl.GetProcAddress(sym);
    }

    public void SwapBuffers()
    {
        _contextDispose.Dispose();
        GlControl.SwapBuffers().Wait();
        MakeCurrent();
    }
    
    
}