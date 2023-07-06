using System.Threading.Tasks;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.Rendering.Composition;

namespace GpuInteropTest.Controls.Helpers;

internal class GlBufferQueue : BufferQueue<GlQueueableImage>
{
    private IOpenGlTextureSharingRenderInterfaceContextFeature _glSharing;
    private IGlContext _glContext;
    
    public GlBufferQueue(ICompositionGpuInterop interop, IOpenGlTextureSharingRenderInterfaceContextFeature glSharing, IGlContext glContext) : base(interop)
    {
        _glSharing = glSharing;
        _glContext = glContext;

    }
    
    protected override GlQueueableImage InitBuffer(PixelSize size)
    {
        return new GlQueueableImage(size, _glContext, _glSharing);
    }

}

internal class GlQueueableImage : IQueuableImage
{
    private ICompositionImportableOpenGlSharedTexture _texture;
    
    public GlQueueableImage(PixelSize size, IGlContext context,
        IOpenGlTextureSharingRenderInterfaceContextFeature glSharing)
    {
        _texture = glSharing.CreateSharedTextureForComposition(context, size);
    }

    public ValueTask DisposeAsync()
    {
        _texture.Dispose();
        return ValueTask.CompletedTask;
    }

    public uint TextureObject => (uint) _texture.TextureId;

    public PixelSize Size => _texture.Size;

    public ICompositionImportedGpuImage Import(ICompositionGpuInterop interop)
    {
        return interop.ImportImage(_texture);
    }
}