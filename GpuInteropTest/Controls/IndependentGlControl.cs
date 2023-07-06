using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Rendering.Composition;
using GpuInteropTest.Controls.Helpers;
using Silk.NET.OpenGL;

namespace GpuInteropTest.Controls;

/// <summary>
/// A control whose contents are controlled via an external render loop.
/// </summary>
public class IndependentGlControl : CompositionControl
{
    // Composition objects
    private IGlContext? _glContext;
    private IOpenGlTextureSharingRenderInterfaceContextFeature? _sharingFeature;
    private GlBufferQueue? _queue;
    private Action _doCompositionUpdate;
    private bool _updateQueued;

    // OpenGL objects
    private uint _renderFbo;
    private uint _depthRbo;

    // Option variables
    private int _glVersionMajor;
    private int _glVersionMinor;
    private GlProfileType _glProfileType;
    private bool _vSync;

    public static readonly DirectProperty<IndependentGlControl, int> GlVersionMajorProperty =
        AvaloniaProperty.RegisterDirect<IndependentGlControl, int>(nameof(GlVersionMajor), c => c._glVersionMajor,
            (c, value) => c._glVersionMajor = value);

    public static readonly DirectProperty<IndependentGlControl, int> GlVersionMinorProperty =
        AvaloniaProperty.RegisterDirect<IndependentGlControl, int>(nameof(GlVersionMinor), c => c._glVersionMinor,
            (c, value) => c._glVersionMinor = value);

    public static readonly DirectProperty<IndependentGlControl, GlProfileType> GlProfileTypeProperty =
        AvaloniaProperty.RegisterDirect<IndependentGlControl, GlProfileType>(nameof(GlProfileType),
            c => c._glProfileType, (c, value) => c._glProfileType = value);

    public static readonly DirectProperty<IndependentGlControl, bool> VSyncProperty =
        AvaloniaProperty.RegisterDirect<IndependentGlControl, bool>(nameof(VSync), c => c._vSync,
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

    public IndependentGlControl()
    {
        _doCompositionUpdate = DoCompositionUpdate;
        _updateQueued = false;

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

        _queue = new GlBufferQueue(Interop!, _sharingFeature, _glContext);

        using (_glContext.MakeCurrent())
        {
            GL gl = GL.GetApi(_glContext.GlInterface.GetProcAddress);
            _renderFbo = gl.GenFramebuffer();
        }
    }

    /// <inheritdoc/>
    protected override async Task FreeGpuResources()
    {
        if (_glContext == null)
            return;
        if (_queue != null)
        {
            await _queue.DisposeAsync();
            _queue = null;
        }
    }

    public nint GetProcAddress(string sym) =>
        _glContext == null ? IntPtr.Zero : _glContext.GlInterface.GetProcAddress(sym);

    public void MakeContextCurrent()
    {
        _glContext?.MakeCurrent();
    }

    public void SwapBuffers()
    {
        if (_queue == null || _glContext == null)
            return;

        _queue.SwapBuffers(WindowSize);

        var curr = _queue.CurrentBuffer;
        // ASSUME that the context is current
        var gl = GL.GetApi(_glContext.GlInterface.GetProcAddress);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _renderFbo);
        // update the depth RBO
        {
            var oldRenderbuffer = (uint) gl.GetInteger(GLEnum.Renderbuffer);
            var depthFormat = _glContext.Version.Type == GlProfileType.OpenGLES
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
        
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, GLEnum.Texture2D,
            curr.TextureObject, 0);

        if (gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != GLEnum.FramebufferComplete)
        {
            throw new ApplicationException("Framebuffer dead");
        }
    }

    private async void DoCompositionUpdate()
    {
        await _queue!.DisplayNext(Surface!);
        Compositor?.RequestCompositionUpdate(_doCompositionUpdate);
    }

    public void InitRenderLoop()
    {
        if (!_updateQueued && InitTask is { Status: TaskStatus.RanToCompletion })
        {
            Compositor?.RequestCompositionUpdate(_doCompositionUpdate);
        }
    }
}