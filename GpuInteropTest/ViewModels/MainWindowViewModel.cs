using System;
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

    
    private const float freq = MathF.Tau / 2000.0f;

    [DoesNotReturn]
    private void OpenGLRun()
    {
        _openGlContextService.MakeCurrent();
        GL gl = GL.GetApi(_openGlContextService.GetProcAddress);

        long t0 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        gl.ClearColor(1.0f, 0.5f, 0.0f, 1.0f);
        while (true)
        {
            long tn = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            float scale = MathF.Sin((tn - t0) * freq);
            // sin^2(x) = 1/2 (1 - cos(2x)), which is always >0
            scale *= scale;
            gl.ClearColor(1.0f * scale, 0.5f * scale, 0.0f, 1.0f);
            
            gl.Clear(ClearBufferMask.ColorBufferBit);
            _openGlContextService.SwapBuffers();
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private Thread? _openGLThread;
}