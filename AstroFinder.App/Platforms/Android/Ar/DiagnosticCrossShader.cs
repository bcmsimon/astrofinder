using Android.Opengl;
using Java.Nio;

namespace AstroFinder.App.Platforms.Android.Ar;

internal static class DiagnosticCrossShader
{
    private static readonly float[] VertexData =
    [
        // Horizontal bar.
        -0.04f, -0.005f, 0f,
         0.04f, -0.005f, 0f,
        -0.04f,  0.005f, 0f,
        -0.04f,  0.005f, 0f,
         0.04f, -0.005f, 0f,
         0.04f,  0.005f, 0f,

        // Vertical bar.
        -0.005f, -0.04f, 0f,
         0.005f, -0.04f, 0f,
        -0.005f,  0.04f, 0f,
        -0.005f,  0.04f, 0f,
         0.005f, -0.04f, 0f,
         0.005f,  0.04f, 0f,
    ];

    private static FloatBuffer? _vertexBuffer;
    private static int _program;
    private static int _positionHandle;
    private static int _mvpHandle;
    private static int _colorHandle;

    public static void Initialize()
    {
        if (_program != 0)
        {
            return;
        }

        _vertexBuffer = ByteBuffer
            .AllocateDirect(VertexData.Length * sizeof(float))
            .Order(ByteOrder.NativeOrder()!)
            .AsFloatBuffer();
        _vertexBuffer.Put(VertexData);
        _vertexBuffer.Position(0);

        var vertexShader = LoadShader(GLES20.GlVertexShader, @"
attribute vec3 aPosition;
uniform mat4 uMvp;
void main() {
    gl_Position = uMvp * vec4(aPosition, 1.0);
}");

        var fragmentShader = LoadShader(GLES20.GlFragmentShader, @"
precision mediump float;
uniform vec4 uColor;
void main() {
    gl_FragColor = uColor;
}");

        _program = GLES20.GlCreateProgram();
        GLES20.GlAttachShader(_program, vertexShader);
        GLES20.GlAttachShader(_program, fragmentShader);
        GLES20.GlLinkProgram(_program);

        _positionHandle = GLES20.GlGetAttribLocation(_program, "aPosition");
        _mvpHandle = GLES20.GlGetUniformLocation(_program, "uMvp");
        _colorHandle = GLES20.GlGetUniformLocation(_program, "uColor");
    }

    public static void Draw(float[] viewMatrix, float[] projectionMatrix, float[] modelMatrix)
    {
        if (_program == 0 || _vertexBuffer == null)
        {
            return;
        }

        var viewModel = new float[16];
        var mvp = new float[16];
        Matrix.MultiplyMM(viewModel, 0, viewMatrix, 0, modelMatrix, 0);
        Matrix.MultiplyMM(mvp, 0, projectionMatrix, 0, viewModel, 0);

        GLES20.GlUseProgram(_program);
        GLES20.GlEnable(GLES20.GlBlend);
        GLES20.GlBlendFunc(GLES20.GlSrcAlpha, GLES20.GlOneMinusSrcAlpha);

        _vertexBuffer.Position(0);
        GLES20.GlVertexAttribPointer(_positionHandle, 3, GLES20.GlFloat, false, 3 * sizeof(float), _vertexBuffer);
        GLES20.GlEnableVertexAttribArray(_positionHandle);
        GLES20.GlUniformMatrix4fv(_mvpHandle, 1, false, mvp, 0);
        GLES20.GlUniform4f(_colorHandle, 1f, 0.12f, 0.12f, 1f);
        GLES20.GlDrawArrays(GLES20.GlTriangles, 0, VertexData.Length / 3);

        GLES20.GlDisableVertexAttribArray(_positionHandle);
        GLES20.GlDisable(GLES20.GlBlend);
    }

    private static int LoadShader(int shaderType, string source)
    {
        var shader = GLES20.GlCreateShader(shaderType);
        GLES20.GlShaderSource(shader, source);
        GLES20.GlCompileShader(shader);
        return shader;
    }
}