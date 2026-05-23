using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.OpenGL;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.OpenGl;

/// <summary>Uploads and draws line layers with a simple MVP shader.</summary>
internal sealed class OpenGlLineRenderer : IDisposable
{
    const int FloatsPerVertex = 3;
    const int GlLines = 0x0001;
    const int GlTriangles = 0x0004;

    const int PositionAttrib = 0;

    int _program;
    int _uMvp;
    int _uColorR;
    int _uColorG;
    int _uColorB;
    int _uColorA;
    int _aPosition;
    bool _initialized;
    GlLineWidth? _lineWidth;
    readonly List<LayerGpu> _layers = [];

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void GlLineWidth(float width);

    sealed class LayerGpu
    {
        public required int Vbo { get; init; }
        public required int VertexCount { get; init; }
        public required Color Color { get; init; }
        public required ViewerPrimitiveKind PrimitiveKind { get; init; }
        public required float LineWidth { get; init; }
    }

    public void Initialize(GlInterface gl)
    {
        if (_initialized)
            return;

        _program = CreateProgram(gl);
        _uMvp = gl.GetUniformLocationString(_program, "uMvp");
        _uColorR = gl.GetUniformLocationString(_program, "uColorR");
        _uColorG = gl.GetUniformLocationString(_program, "uColorG");
        _uColorB = gl.GetUniformLocationString(_program, "uColorB");
        _uColorA = gl.GetUniformLocationString(_program, "uColorA");
        _aPosition = gl.GetAttribLocationString(_program, "aPosition");
        if (_aPosition != PositionAttrib)
            throw new InvalidOperationException($"aPosition must be location {PositionAttrib}, got {_aPosition}");

        var lineWidthPtr = gl.GetProcAddress("glLineWidth");
        if (lineWidthPtr != IntPtr.Zero)
            _lineWidth = Marshal.GetDelegateForFunctionPointer<GlLineWidth>(lineWidthPtr);

        _initialized = true;
    }

    public void Dispose()
    {
        _layers.Clear();
        _initialized = false;
        _program = 0;
        _lineWidth = null;
    }

    public void Deinitialize(GlInterface gl)
    {
        if (!_initialized)
            return;

        gl.DeleteProgram(_program);
        foreach (var layer in _layers)
            gl.DeleteBuffer(layer.Vbo);
        _layers.Clear();
        _initialized = false;
        _program = 0;
        _lineWidth = null;
    }

    public void SetScene(GlInterface gl, IEnumerable<ViewerLineLayer> layers)
    {
        if (!_initialized)
            return;

        foreach (var layer in _layers)
            gl.DeleteBuffer(layer.Vbo);
        _layers.Clear();

        foreach (var layer in layers)
        {
            var vertices = LayerVertices(layer.Points);
            if (!IsDrawable(layer, vertices.Length))
                continue;

            var vbo = gl.GenBuffer();
            gl.BindBuffer(GlConsts.GL_ARRAY_BUFFER, vbo);
            UploadVertices(gl, vertices);
            _layers.Add(new LayerGpu
            {
                Vbo = vbo,
                VertexCount = vertices.Length / FloatsPerVertex,
                Color = layer.Color,
                PrimitiveKind = layer.PrimitiveKind,
                LineWidth = Math.Max(1f, layer.LineWidth),
            });
        }
    }

    public void Draw(GlInterface gl, Matrix4x4 mvp)
    {
        if (!_initialized || _layers.Count == 0)
            return;

        gl.UseProgram(_program);
        UploadMvp(gl, mvp);

        foreach (var layer in _layers)
        {
            var rgba = layer.Color;
            SetColor(gl, rgba.R / 255f, rgba.G / 255f, rgba.B / 255f, rgba.A / 255f);
            _lineWidth?.Invoke(layer.LineWidth);

            gl.BindBuffer(GlConsts.GL_ARRAY_BUFFER, layer.Vbo);
            gl.EnableVertexAttribArray(PositionAttrib);
            gl.VertexAttribPointer(PositionAttrib, 3, GlConsts.GL_FLOAT, 0, 0, IntPtr.Zero);
            gl.DrawArrays(ToGlPrimitive(layer.PrimitiveKind), 0, (IntPtr)layer.VertexCount);
        }

        _lineWidth?.Invoke(1f);
        gl.BindBuffer(GlConsts.GL_ARRAY_BUFFER, 0);
        gl.UseProgram(0);
    }

    static bool IsDrawable(ViewerLineLayer layer, int vertexFloatCount) =>
        layer.PrimitiveKind switch
        {
            ViewerPrimitiveKind.Triangles => vertexFloatCount >= FloatsPerVertex * 3,
            _ => vertexFloatCount >= FloatsPerVertex * 2,
        };

    static int ToGlPrimitive(ViewerPrimitiveKind primitiveKind) =>
        primitiveKind == ViewerPrimitiveKind.Triangles ? GlTriangles : GlLines;

    static void UploadVertices(GlInterface gl, float[] vertices)
    {
        unsafe
        {
            fixed (float* ptr = vertices)
            {
                var byteLen = new IntPtr(vertices.Length * sizeof(float));
                gl.BufferData(GlConsts.GL_ARRAY_BUFFER, byteLen, new IntPtr(ptr), GlConsts.GL_STATIC_DRAW);
            }
        }
    }

    void UploadMvp(GlInterface gl, Matrix4x4 matrix)
    {
        if (_uMvp < 0)
            return;

        // Match Avalonia OpenGlContent: System.Numerics row-major, transpose=false.
        unsafe
        {
            gl.UniformMatrix4fv(_uMvp, 1, false, &matrix);
        }
    }

    void SetColor(GlInterface gl, float r, float g, float b, float a)
    {
        if (_uColorR >= 0) gl.Uniform1f(_uColorR, r);
        if (_uColorG >= 0) gl.Uniform1f(_uColorG, g);
        if (_uColorB >= 0) gl.Uniform1f(_uColorB, b);
        if (_uColorA >= 0) gl.Uniform1f(_uColorA, a);
    }

    static float[] LayerVertices(IReadOnlyList<NumericVector3> points)
    {
        var verts = new float[points.Count * FloatsPerVertex];
        for (var i = 0; i < points.Count; i++)
        {
            verts[i * FloatsPerVertex] = points[i].X;
            verts[i * FloatsPerVertex + 1] = points[i].Y;
            verts[i * FloatsPerVertex + 2] = points[i].Z;
        }

        return verts;
    }

    static int CreateProgram(GlInterface gl)
    {
        const string vertexShader = """
            #version 300 es
            precision mediump float;
            uniform mat4 uMvp;
            in vec3 aPosition;
            void main() {
                gl_Position = uMvp * vec4(aPosition, 1.0);
            }
            """;

        const string fragmentShader = """
            #version 300 es
            precision mediump float;
            uniform float uColorR;
            uniform float uColorG;
            uniform float uColorB;
            uniform float uColorA;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(uColorR, uColorG, uColorB, uColorA);
            }
            """;

        var vs = gl.CreateShader(GlConsts.GL_VERTEX_SHADER);
        var fs = gl.CreateShader(GlConsts.GL_FRAGMENT_SHADER);
        var vsErr = gl.CompileShaderAndGetError(vs, vertexShader);
        if (vsErr != null)
            throw new InvalidOperationException("Vertex shader: " + vsErr);
        var fsErr = gl.CompileShaderAndGetError(fs, fragmentShader);
        if (fsErr != null)
            throw new InvalidOperationException("Fragment shader: " + fsErr);

        var program = gl.CreateProgram();
        gl.AttachShader(program, vs);
        gl.AttachShader(program, fs);
        gl.BindAttribLocationString(program, PositionAttrib, "aPosition");
        var linkErr = gl.LinkProgramAndGetError(program);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        if (linkErr != null)
            throw new InvalidOperationException("Link: " + linkErr);
        return program;
    }
}
