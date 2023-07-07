using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace GpuInteropTest.Controls.Helpers;

/// <summary>
/// Enqueues and displays OpenGL buffers.
/// </summary>
public class GLBufferQueue : IBufferQueue<GLQueuableImage>
{
    private Queue<GLQueuableImage> _pendingBuffers;
    private GLQueuableImage? _currentBuffer;

    private Compositor _compositor;
    private ICompositionGpuInterop _interop;
    private CompositionDrawingSurface _target;
    private IOpenGlTextureSharingRenderInterfaceContextFeature _glSharing;
    private IGlContext _glContext;


    public GLBufferQueue(Compositor compositor, ICompositionGpuInterop interop, CompositionDrawingSurface target, IOpenGlTextureSharingRenderInterfaceContextFeature glSharing, IGlContext glContext)
    {
        _compositor = compositor;
        _interop = interop;
        _target = target;
        _glSharing = glSharing;
        _glContext = glContext;
        _currentBuffer = null;
        _pendingBuffers = new Queue<GLQueuableImage>();
    }

    public GLQueuableImage CurrentBuffer
    {
        get
        {
            if (_currentBuffer == null)
                throw new InvalidOperationException($"Call {nameof(SwapBuffers)} to initialize the current buffer.");
            return _currentBuffer;
        }
    }
    public bool VSync { get; set; }

    private async Task DisplayNext(GLQueuableImage img)
    {
        ICompositionImportedGpuImage? import = null;
        try
        {
            if (VSync)
                await _compositor.NextVSync();
            import = img.Import(_interop);
            await import.ImportCompeted;
            await _target.UpdateAsync(import);
        }
        finally
        {
            if (import != null)
                await import.DisposeAsync();
        }
    }

#pragma warning disable CS4014
    public async Task SwapBuffers(PixelSize size)
    {
        if (_currentBuffer != null)
        {
            Console.WriteLine("wait display");
            await Dispatcher.UIThread.Invoke(async () => await DisplayNext(_currentBuffer));
            if (_currentBuffer.Size == size)
            {
                _pendingBuffers.Enqueue(_currentBuffer);
                Console.WriteLine("return");
            }
            else
            {
                _currentBuffer.DisposeAsync();
                Console.WriteLine("no return");
            }
        }
        // pull new buffer from incoming queue
        if (_pendingBuffers.Count < 3)
        {
            Console.WriteLine("new");
            _currentBuffer = new GLQueuableImage(size, _glSharing, _glContext);
        }
        else
        {
            Console.WriteLine("old");
            _currentBuffer = _pendingBuffers.Dequeue();
        }
    }
#pragma warning restore CS4014

    public ValueTask DisposeAsync()
    {
        throw new System.NotImplementedException();
    }
}