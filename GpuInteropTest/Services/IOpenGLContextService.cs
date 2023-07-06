using Avalonia;

namespace GpuInteropTest.Services;

public interface IOpenGLContextService
{
    PixelSize ViewportSize { get; set; }
    
    bool VSync { get; set; }

    void MakeCurrent();

    nint GetProcAddress(string sym);

    void SwapBuffers();
}