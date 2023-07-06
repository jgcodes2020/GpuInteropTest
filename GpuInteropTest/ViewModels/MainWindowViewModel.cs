using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Threading;
using Avalonia;
using GpuInteropTest.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Silk.NET.OpenGL;

namespace GpuInteropTest.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IOpenGLContextService _openGlContextService;

    public MainWindowViewModel(IOpenGLContextService openGlContextService)
    {
        _openGlContextService = openGlContextService;
        StartOpenGLCommand = ReactiveCommand.Create(() =>
        {
            if (_openGLThread != null)
                return;
            _openGlContextService.VSync = true;
            _openGlContextService.ViewportSize = new PixelSize(640, 480);
            
            _openGLThread = new Thread(OpenGLRun);
            _openGLThread.Start();
            
        });
    }
    
    [Reactive]
    public bool ShowLabel { get; set; }

    public ReactiveCommand<Unit, Unit> StartOpenGLCommand { get; }

    [DoesNotReturn]
    private void OpenGLRun()
    {
        _openGlContextService.MakeCurrent();

        GL gl = GL.GetApi(_openGlContextService.GetProcAddress);
        gl.ClearColor(1.0f, 0.5f, 0.0f, 1.0f);
        while (true)
        {
            gl.Clear(ClearBufferMask.ColorBufferBit);
            _openGlContextService.SwapBuffers();
        }
    }

    private Thread? _openGLThread;
}