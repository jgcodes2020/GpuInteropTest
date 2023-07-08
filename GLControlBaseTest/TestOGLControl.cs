using System;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;

namespace GLControlBaseTest;

public class TestOGLControl : OpenGlControlBase
{
    private const float freq = MathF.Tau / 2000.0f;
    
    private long _start;
    protected override void OnOpenGlInit(GlInterface gl)
    {
        _start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    protected override void OnOpenGlRender(GlInterface glInterface, int fb)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var mulc = MathF.Sin((now - _start) * freq);
        mulc *= mulc;
        var gl = GL.GetApi(glInterface.GetProcAddress);
        
        gl.ClearColor(1.0f * mulc, 0.5f * mulc, 0.0f, 1.0f);
        gl.Clear(ClearBufferMask.ColorBufferBit);
        
        RequestNextFrameRendering();
    }
}