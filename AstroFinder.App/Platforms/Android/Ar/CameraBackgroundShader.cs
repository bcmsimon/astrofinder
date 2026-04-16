using Android.Opengl;

namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// Minimal OpenGL ES 2.0 helper that draws the ARCore camera background
/// as a fullscreen textured quad using an external OES texture.
/// </summary>
internal static class CameraBackgroundShader
{
    private const string VertexShaderSource = @"
        attribute vec4 a_Position;
        attribute vec2 a_TexCoord;
        varying vec2 v_TexCoord;
        void main() {
            gl_Position = a_Position;
            v_TexCoord = a_TexCoord;
        }";

    private const string FragmentShaderSource = @"
        #extension GL_OES_EGL_image_external : require
        precision mediump float;
        varying vec2 v_TexCoord;
        uniform samplerExternalOES u_Texture;
        void main() {
            gl_FragColor = texture2D(u_Texture, v_TexCoord);
        }";

    // Fullscreen quad: two triangles covering NDC [-1,1].
    private static readonly float[] QuadPositions =
    {
        -1f, -1f,
         1f, -1f,
        -1f,  1f,
         1f,  1f,
    };

    // Default UVs (may be overridden by ARCore's texture transform).
    private static readonly float[] QuadTexCoords =
    {
        0f, 1f,
        1f, 1f,
        0f, 0f,
        1f, 0f,
    };

    private static int _program;
    private static int _positionAttrib;
    private static int _texCoordAttrib;

    // Pre-allocated buffers — avoids native memory leak from AllocateDirect every frame.
    private static Java.Nio.FloatBuffer? _positionBuffer;
    private static Java.Nio.FloatBuffer? _texCoordBuffer;

    public static void Initialize()
    {
        int vertexShader = LoadShader(GLES20.GlVertexShader, VertexShaderSource);
        int fragmentShader = LoadShader(GLES20.GlFragmentShader, FragmentShaderSource);

        _program = GLES20.GlCreateProgram();
        GLES20.GlAttachShader(_program, vertexShader);
        GLES20.GlAttachShader(_program, fragmentShader);
        GLES20.GlLinkProgram(_program);

        _positionAttrib = GLES20.GlGetAttribLocation(_program, "a_Position");
        _texCoordAttrib = GLES20.GlGetAttribLocation(_program, "a_TexCoord");

        // Pre-allocate reusable native buffers.
        _positionBuffer = AllocateFloatBuffer(QuadPositions.Length);
        _texCoordBuffer = AllocateFloatBuffer(8); // 4 vertices * 2 components
    }

    public static void Draw(int textureId, float[] transformedUvs)
    {
        if (_positionBuffer == null || _texCoordBuffer == null) return;

        GLES20.GlDisable(GLES20.GlDepthTest);
        GLES20.GlDepthMask(false);

        GLES20.GlUseProgram(_program);

        GLES20.GlActiveTexture(GLES20.GlTexture0);
        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, textureId);

        // Reuse pre-allocated position buffer.
        _positionBuffer.Clear();
        _positionBuffer.Put(QuadPositions);
        _positionBuffer.Position(0);
        GLES20.GlVertexAttribPointer(_positionAttrib, 2, GLES20.GlFloat, false, 0, _positionBuffer);
        GLES20.GlEnableVertexAttribArray(_positionAttrib);

        // Reuse pre-allocated UV buffer with ARCore-provided transformed UVs.
        var uvs = transformedUvs ?? QuadTexCoords;
        _texCoordBuffer.Clear();
        _texCoordBuffer.Put(uvs);
        _texCoordBuffer.Position(0);
        GLES20.GlVertexAttribPointer(_texCoordAttrib, 2, GLES20.GlFloat, false, 0, _texCoordBuffer);
        GLES20.GlEnableVertexAttribArray(_texCoordAttrib);

        GLES20.GlDrawArrays(GLES20.GlTriangleStrip, 0, 4);

        GLES20.GlDisableVertexAttribArray(_positionAttrib);
        GLES20.GlDisableVertexAttribArray(_texCoordAttrib);

        GLES20.GlDepthMask(true);
        GLES20.GlEnable(GLES20.GlDepthTest);
    }

    public static int CreateExternalTexture()
    {
        var textures = new int[1];
        GLES20.GlGenTextures(1, textures, 0);
        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, textures[0]);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMinFilter, GLES20.GlLinear);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMagFilter, GLES20.GlLinear);
        return textures[0];
    }

    private static int LoadShader(int type, string source)
    {
        int shader = GLES20.GlCreateShader(type);
        GLES20.GlShaderSource(shader, source);
        GLES20.GlCompileShader(shader);
        return shader;
    }

    private static Java.Nio.FloatBuffer AllocateFloatBuffer(int floatCount)
    {
        var bb = Java.Nio.ByteBuffer.AllocateDirect(floatCount * 4);
        bb.Order(Java.Nio.ByteOrder.NativeOrder()!);
        return bb.AsFloatBuffer();
    }
}
