using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Rendering.Composition;
using GpuInteropTest.Controls.Helpers;
using Silk.NET.OpenGL;

namespace GpuInteropTest.Controls;

/// <summary>
/// A control whose contents are controlled via an external OpenGL render loop.
/// </summary>
public class IndependentGLControl : CompositionControl
{
    // Composition objects
    private IGlContext? _glContext;
    private IOpenGlTextureSharingRenderInterfaceContextFeature? _sharingFeature;
    private GLBufferQueue? _bufferQueue;

    // OpenGL objects
    private uint _renderFbo;
    private uint _depthRbo;

    // Option variables
    private int _glVersionMajor;
    private int _glVersionMinor;
    private GlProfileType _glProfileType;
    private bool _vSync;

    public static readonly DirectProperty<IndependentGLControl, int> GlVersionMajorProperty =
        AvaloniaProperty.RegisterDirect<IndependentGLControl, int>(nameof(GlVersionMajor), c => c._glVersionMajor,
            (c, value) => c._glVersionMajor = value);

    public static readonly DirectProperty<IndependentGLControl, int> GlVersionMinorProperty =
        AvaloniaProperty.RegisterDirect<IndependentGLControl, int>(nameof(GlVersionMinor), c => c._glVersionMinor,
            (c, value) => c._glVersionMinor = value);

    public static readonly DirectProperty<IndependentGLControl, GlProfileType> GlProfileTypeProperty =
        AvaloniaProperty.RegisterDirect<IndependentGLControl, GlProfileType>(nameof(GlProfileType),
            c => c._glProfileType, (c, value) => c._glProfileType = value);

    public static readonly DirectProperty<IndependentGLControl, bool> VSyncProperty =
        AvaloniaProperty.RegisterDirect<IndependentGLControl, bool>(nameof(VSync), c => c._vSync,
            (c, value) => c._vSync = value);

    // Option values

    public GlVersion GlVersion => new(_glProfileType, GlVersionMajor, GlVersionMinor);

    public bool VSync
    {
        get => _vSync;
        set => SetAndRaise(VSyncProperty, ref _vSync, value);
    }

    public int GlVersionMajor
    {
        get => _glVersionMajor;
        set => SetAndRaise(GlVersionMajorProperty, ref _glVersionMajor, value);
    }

    public int GlVersionMinor
    {
        get => _glVersionMinor;
        set => SetAndRaise(GlVersionMinorProperty, ref _glVersionMinor, value);
    }

    public GlProfileType GlProfileType
    {
        get => _glProfileType;
        set => SetAndRaise(GlProfileTypeProperty, ref _glProfileType, value);
    }

    public IndependentGLControl()
    {

        _renderFbo = _depthRbo = 0;
    }

    private PixelSize ComputePixelSize(Size size)
    {
        double scaling = VisualRoot!.RenderScaling;
        return new PixelSize(Math.Max(1, (int) (size.Width * scaling)), Math.Max(1, (int) (size.Height * scaling)));
    }

    /// <inheritdoc/>
    protected override async Task InitGpuResources(Compositor compositor, CompositionDrawingSurface surface,
        ICompositionGpuInterop interop)
    {
        _sharingFeature =
            await compositor.GetRenderInterfaceFeature<IOpenGlTextureSharingRenderInterfaceContextFeature>();
        if (!_sharingFeature.CanCreateSharedContext)
            throw new PlatformNotSupportedException("Can't create shared context");

        _glContext = _sharingFeature.CreateSharedContext(new[] { GlVersion }) ??
                     throw new ApplicationException("Couldn't create shared context");

        _bufferQueue = new GLBufferQueue(compositor, interop, surface, _sharingFeature, _glContext);

        using (_glContext.MakeCurrent())
        {
            GL gl = GL.GetApi(_glContext.GlInterface.GetProcAddress);
            _renderFbo = gl.GenFramebuffer();
        }

        await SwapBuffers();
    }

    /// <inheritdoc/>
    protected override async Task FreeGpuResources()
    {
        if (_glContext == null)
            return;
        if (_bufferQueue != null)
        {
            await _bufferQueue.DisposeAsync();
            _bufferQueue = null;
        }
    }

    public nint GetProcAddress(string sym) =>
        _glContext == null ? IntPtr.Zero : _glContext.GlInterface.GetProcAddress(sym);

    public IDisposable? MakeContextCurrent()
    {
        return _glContext?.MakeCurrent();
    }

    private void InitGLBuffers(GL gl, GLQueuableImage buffer)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderFbo);
        {
            var oldRenderbuffer = (uint) gl.GetInteger(GLEnum.Renderbuffer);
            var depthFormat = _glContext!.Version.Type == GlProfileType.OpenGLES
                ? InternalFormat.DepthComponent16
                : InternalFormat.DepthComponent;

            try
            {
                if (_depthRbo != 0)
                {
                    gl.DeleteRenderbuffer(_depthRbo);
                }

                _depthRbo = gl.GenRenderbuffer();
                gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRbo);
                gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, depthFormat,
                    (uint) WindowSize.Width, (uint) WindowSize.Height);
                gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _depthRbo);
            }
            finally
            {
                gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, oldRenderbuffer);
            }
        }
        
        // Attach the new buffer to the FBO, and make sure it's working
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, GLEnum.Texture2D,
            buffer.TextureObject, 0);
        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            throw new ApplicationException("Framebuffer dead");
        }
    }

    /// <summary>
    /// Swaps buffers for the rendering thread.
    /// </summary>
    public async Task SwapBuffers()
    {
        Console.WriteLine("Swap");
        if (_bufferQueue == null || _glContext == null)
            return;
        
        // ASSUME that the context is current
        var gl = GL.GetApi(_glContext.GlInterface.GetProcAddress);
        
        gl.Flush();
        
        await _bufferQueue.SwapBuffers(WindowSize);
        var curr = _bufferQueue.CurrentBuffer;
        
        InitGLBuffers(gl, curr);
    }
}