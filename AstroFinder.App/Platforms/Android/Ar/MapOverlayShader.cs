using Android.Opengl;

namespace AstroFinder.App.Platforms.Android.Ar;

/// <summary>
/// OpenGL ES 2.0 shader that draws a textured quad in world space
/// using ARCore's view and projection matrices, with alpha blending.
/// Used to render the star hop map bitmap anchored in AR space.
/// </summary>
internal static class MapOverlayShader
{
    private const string VertexShaderSource = @"
        uniform mat4 u_MVP;
        attribute vec4 a_Position;
        attribute vec2 a_TexCoord;
        varying vec2 v_TexCoord;
        void main() {
            gl_Position = u_MVP * a_Position;
            v_TexCoord = a_TexCoord;
        }";

    private const string FragmentShaderSource = @"
        precision mediump float;
        varying vec2 v_TexCoord;
        uniform sampler2D u_Texture;
        uniform float u_Alpha;
        void main() {
            vec4 color = texture2D(u_Texture, v_TexCoord);
            gl_FragColor = vec4(color.rgb, color.a * u_Alpha);
        }";

    private static int _program;
    private static int _positionAttrib;
    private static int _texCoordAttrib;
    private static int _mvpUniform;
    private static int _textureUniform;
    private static int _alphaUniform;

    private static Java.Nio.FloatBuffer? _positionBuffer;
    private static Java.Nio.FloatBuffer? _texCoordBuffer;
    private static Java.Nio.ShortBuffer? _indexBuffer;

    // Quad vertices: unit square centered at origin in XY plane.
    // Will be scaled/rotated by model matrix to cover correct sky area.
    private static readonly float[] QuadPositions =
    {
        -0.5f, -0.5f, 0f,
         0.5f, -0.5f, 0f,
         0.5f,  0.5f, 0f,
        -0.5f,  0.5f, 0f,
    };

    private static readonly float[] QuadTexCoords =
    {
        0f, 1f,
        1f, 1f,
        1f, 0f,
        0f, 0f,
    };

    private static readonly short[] QuadIndices = { 0, 1, 2, 0, 2, 3 };

    private static int _textureId;
    private static bool _textureReady;

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
        _mvpUniform = GLES20.GlGetUniformLocation(_program, "u_MVP");
        _textureUniform = GLES20.GlGetUniformLocation(_program, "u_Texture");
        _alphaUniform = GLES20.GlGetUniformLocation(_program, "u_Alpha");

        _positionBuffer = AllocateFloatBuffer(QuadPositions.Length);
        _texCoordBuffer = AllocateFloatBuffer(QuadTexCoords.Length);

        var ibb = Java.Nio.ByteBuffer.AllocateDirect(QuadIndices.Length * 2);
        ibb.Order(Java.Nio.ByteOrder.NativeOrder()!);
        _indexBuffer = ibb.AsShortBuffer();
        _indexBuffer.Put(QuadIndices);
        _indexBuffer.Position(0);

        // Create the texture.
        var textures = new int[1];
        GLES20.GlGenTextures(1, textures, 0);
        _textureId = textures[0];
    }

    /// <summary>
    /// Upload an ARGB_8888 Android bitmap as the map overlay texture.
    /// Must be called from the GL thread.
    /// </summary>
    public static void UploadBitmap(global::Android.Graphics.Bitmap bitmap)
    {
        GLES20.GlBindTexture(GLES20.GlTexture2d, _textureId);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMinFilter, GLES20.GlLinear);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureMagFilter, GLES20.GlLinear);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
        GLES20.GlTexParameteri(GLES20.GlTexture2d, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);
        GLUtils.TexImage2D(GLES20.GlTexture2d, 0, bitmap, 0);
        _textureReady = true;
    }

    /// <summary>
    /// Draw the map overlay quad in world space.
    /// </summary>
    /// <param name="viewMatrix">4x4 column-major from ARCore camera.getViewMatrix()</param>
    /// <param name="projectionMatrix">4x4 column-major from ARCore camera.getProjectionMatrix()</param>
    /// <param name="modelMatrix">4x4 column-major placing the quad in ARCore world space</param>
    /// <param name="alpha">Overlay transparency (0=invisible, 1=opaque)</param>
    public static void Draw(float[] viewMatrix, float[] projectionMatrix, float[] modelMatrix, float alpha = 0.85f)
    {
        if (!_textureReady || _positionBuffer == null || _texCoordBuffer == null || _indexBuffer == null) return;

        // Compute MVP = projection * view * model.
        float[] mvTemp = new float[16];
        float[] mvp = new float[16];
        global::Android.Opengl.Matrix.MultiplyMM(mvTemp, 0, viewMatrix, 0, modelMatrix, 0);
        global::Android.Opengl.Matrix.MultiplyMM(mvp, 0, projectionMatrix, 0, mvTemp, 0);

        GLES20.GlUseProgram(_program);

        // Enable alpha blending.
        GLES20.GlEnable(GLES20.GlBlend);
        GLES20.GlBlendFunc(GLES20.GlSrcAlpha, GLES20.GlOneMinusSrcAlpha);
        GLES20.GlDisable(GLES20.GlDepthTest);

        GLES20.GlUniformMatrix4fv(_mvpUniform, 1, false, mvp, 0);
        GLES20.GlUniform1i(_textureUniform, 0);
        GLES20.GlUniform1f(_alphaUniform, alpha);

        GLES20.GlActiveTexture(GLES20.GlTexture0);
        GLES20.GlBindTexture(GLES20.GlTexture2d, _textureId);

        _positionBuffer.Clear();
        _positionBuffer.Put(QuadPositions);
        _positionBuffer.Position(0);
        GLES20.GlVertexAttribPointer(_positionAttrib, 3, GLES20.GlFloat, false, 0, _positionBuffer);
        GLES20.GlEnableVertexAttribArray(_positionAttrib);

        _texCoordBuffer.Clear();
        _texCoordBuffer.Put(QuadTexCoords);
        _texCoordBuffer.Position(0);
        GLES20.GlVertexAttribPointer(_texCoordAttrib, 2, GLES20.GlFloat, false, 0, _texCoordBuffer);
        GLES20.GlEnableVertexAttribArray(_texCoordAttrib);

        _indexBuffer.Position(0);
        GLES20.GlDrawElements(GLES20.GlTriangles, QuadIndices.Length, GLES20.GlUnsignedShort, _indexBuffer);

        GLES20.GlDisableVertexAttribArray(_positionAttrib);
        GLES20.GlDisableVertexAttribArray(_texCoordAttrib);

        GLES20.GlDisable(GLES20.GlBlend);
        GLES20.GlEnable(GLES20.GlDepthTest);
    }

    public static void Cleanup()
    {
        if (_textureId != 0)
        {
            GLES20.GlDeleteTextures(1, new[] { _textureId }, 0);
            _textureId = 0;
        }
        _textureReady = false;
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
