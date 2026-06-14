using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.OpenGL;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.OpenGl;

/// <summary>Uploads and draws line layers with a simple MVP shader.</summary>
internal sealed class OpenGlLineRenderer : IDisposable
{
    internal const int MaxVerticesPerUploadChunk = 250_000;
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
    readonly List<LayerGpu> _staticLayers = [];
    readonly List<LayerGpu> _dynamicLayers = [];

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate void GlLineWidth(float width);

    sealed class LayerGpu
    {
        public required int Vbo { get; init; }
        public required int Vao { get; init; }
        public required int VertexCount { get; init; }
        public required Color Color { get; init; }
        public required ViewerPrimitiveKind PrimitiveKind { get; init; }
        public required float LineWidth { get; init; }
    }

    public string? LastError { get; private set; }

    public void Initialize(GlInterface gl, GlVersion version)
    {
        if (_initialized)
            return;

        LastError = null;
        var useOpenGlEs = version.Type == GlProfileType.OpenGLES;
        _program = CreateProgram(gl, useOpenGlEs);
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
        _staticLayers.Clear();
        _dynamicLayers.Clear();
        _initialized = false;
        _program = 0;
        _lineWidth = null;
    }

    public void Deinitialize(GlInterface gl)
    {
        if (!_initialized)
            return;

        gl.DeleteProgram(_program);
        DeleteLayers(gl, _staticLayers);
        DeleteLayers(gl, _dynamicLayers);
        _initialized = false;
        _program = 0;
        _lineWidth = null;
    }

    public bool SetScene(
        GlInterface gl,
        IEnumerable<ViewerLineLayer> staticLayers,
        IEnumerable<ViewerLineLayer> dynamicLayers)
    {
        if (!_initialized)
            return false;

        LastError = null;
        DeleteLayers(gl, _staticLayers);
        DeleteLayers(gl, _dynamicLayers);

        if (!UploadLayers(gl, staticLayers, _staticLayers))
            return false;
        if (!UploadLayers(gl, dynamicLayers, _dynamicLayers))
            return false;

        gl.BindBuffer(GlConsts.GL_ARRAY_BUFFER, 0);
        gl.BindVertexArray(0);

        return true;
    }

    public bool SetDynamicLayers(GlInterface gl, IEnumerable<ViewerLineLayer> dynamicLayers)
    {
        if (!_initialized)
            return false;

        LastError = null;
        DeleteLayers(gl, _dynamicLayers);
        var uploaded = UploadLayers(gl, dynamicLayers, _dynamicLayers);
        gl.BindBuffer(GlConsts.GL_ARRAY_BUFFER, 0);
        gl.BindVertexArray(0);
        return uploaded;
    }

    public bool Draw(GlInterface gl, Matrix4x4 mvp)
    {
        if (!_initialized || (_staticLayers.Count == 0 && _dynamicLayers.Count == 0))
            return true;

        LastError = null;
        gl.UseProgram(_program);
        UploadMvpForRowVectorCamera(gl, mvp);

        DrawLayers(gl, _staticLayers);
        DrawLayers(gl, _dynamicLayers);

        _lineWidth?.Invoke(1f);
        gl.BindVertexArray(0);
        gl.BindBuffer(GlConsts.GL_ARRAY_BUFFER, 0);
        gl.UseProgram(0);
        return LastError == null;
    }

    bool UploadLayers(GlInterface gl, IEnumerable<ViewerLineLayer> source, List<LayerGpu> target)
    {
        foreach (var layer in source)
        {
            var primitiveKind = layer.PrimitiveKind;
            if (!IsDrawable(primitiveKind, layer.Points.Count))
                continue;

            foreach (var (start, count) in ChunkVertices(layer.Points.Count, primitiveKind))
            {
                var vbo = gl.GenBuffer();
                if (vbo == 0)
                {
                    LastError = "OpenGL VBO unavailable";
                    return false;
                }

                var vao = gl.GenVertexArray();
                if (vao == 0)
                {
                    gl.DeleteBuffer(vbo);
                    LastError = "OpenGL VAO unavailable";
                    return false;
                }

                gl.BindVertexArray(vao);
                gl.BindBuffer(GlConsts.GL_ARRAY_BUFFER, vbo);
                UploadVertices(gl, layer.Points, start, count);
                gl.VertexAttribPointer(PositionAttrib, 3, GlConsts.GL_FLOAT, 0, 0, IntPtr.Zero);
                gl.EnableVertexAttribArray(PositionAttrib);

                target.Add(new LayerGpu
                {
                    Vbo = vbo,
                    Vao = vao,
                    VertexCount = count,
                    Color = layer.Color,
                    PrimitiveKind = primitiveKind,
                    LineWidth = Math.Max(1f, layer.LineWidth),
                });
            }
        }

        return true;
    }

    internal static List<(int Start, int Count)> ChunkVertices(int vertexCount, ViewerPrimitiveKind primitiveKind)
    {
        var primitiveVertexCount = PrimitiveVertexCount(primitiveKind);
        var drawableVertexCount = vertexCount - vertexCount % primitiveVertexCount;
        if (drawableVertexCount < primitiveVertexCount)
            return [];

        var maxChunkVertices = MaxVerticesPerUploadChunk - MaxVerticesPerUploadChunk % primitiveVertexCount;
        if (maxChunkVertices < primitiveVertexCount)
            maxChunkVertices = primitiveVertexCount;

        var chunks = new List<(int Start, int Count)>((drawableVertexCount + maxChunkVertices - 1) / maxChunkVertices);
        for (var start = 0; start < drawableVertexCount; start += maxChunkVertices)
        {
            var count = Math.Min(maxChunkVertices, drawableVertexCount - start);
            count -= count % primitiveVertexCount;
            if (count >= primitiveVertexCount)
                chunks.Add((start, count));
        }

        return chunks;
    }

    static int PrimitiveVertexCount(ViewerPrimitiveKind primitiveKind) =>
        primitiveKind == ViewerPrimitiveKind.Triangles ? 3 : 2;

    static bool IsDrawable(ViewerPrimitiveKind primitiveKind, int vertexCount) =>
        vertexCount >= PrimitiveVertexCount(primitiveKind);

    static int ToGlPrimitive(ViewerPrimitiveKind primitiveKind) =>
        primitiveKind == ViewerPrimitiveKind.Triangles ? GlTriangles : GlLines;

    static void UploadVertices(
        GlInterface gl,
        IReadOnlyList<NumericVector3> points,
        int start,
        int count)
    {
        var vertices = new float[count * FloatsPerVertex];
        for (var i = 0; i < count; i++)
        {
            var point = points[start + i];
            var offset = i * FloatsPerVertex;
            vertices[offset] = point.X;
            vertices[offset + 1] = point.Y;
            vertices[offset + 2] = point.Z;
        }

        unsafe
        {
            fixed (float* ptr = vertices)
            {
                var byteLen = new IntPtr(vertices.Length * sizeof(float));
                gl.BufferData(GlConsts.GL_ARRAY_BUFFER, byteLen, new IntPtr(ptr), GlConsts.GL_STATIC_DRAW);
            }
        }
    }

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

    void UploadMvpForRowVectorCamera(GlInterface gl, Matrix4x4 matrix)
    {
        if (_uMvp < 0)
            return;

        // ViewerCamera builds a System.Numerics row-vector MVP (view * projection).
        // Avalonia's GL samples upload that layout with transpose=false, so keep it explicit here.
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

    static int CreateProgram(GlInterface gl, bool useOpenGlEs)
    {
        var primaryErr = TryCreateProgram(gl, useOpenGlEs, out var program);
        if (program != 0)
            return program;

        var fallbackErr = TryCreateProgram(gl, !useOpenGlEs, out program);
        if (program != 0)
            return program;

        throw new InvalidOperationException(primaryErr ?? fallbackErr ?? "Shader link failed");
    }

    static string? TryCreateProgram(GlInterface gl, bool useOpenGlEs, out int program)
    {
        program = 0;
        var (vertexShader, fragmentShader) = useOpenGlEs ? OpenGlEsShaders() : DesktopGlShaders();

        var vs = gl.CreateShader(GlConsts.GL_VERTEX_SHADER);
        var fs = gl.CreateShader(GlConsts.GL_FRAGMENT_SHADER);
        var vsErr = gl.CompileShaderAndGetError(vs, vertexShader);
        if (vsErr != null)
        {
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            return "Vertex shader: " + vsErr;
        }

        var fsErr = gl.CompileShaderAndGetError(fs, fragmentShader);
        if (fsErr != null)
        {
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
            return "Fragment shader: " + fsErr;
        }

        program = gl.CreateProgram();
        gl.AttachShader(program, vs);
        gl.AttachShader(program, fs);
        gl.BindAttribLocationString(program, PositionAttrib, "aPosition");
        var linkErr = gl.LinkProgramAndGetError(program);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        if (linkErr != null)
        {
            gl.DeleteProgram(program);
            program = 0;
            return "Link: " + linkErr;
        }

        return null;
    }

    void DrawLayers(GlInterface gl, List<LayerGpu> layers)
    {
        foreach (var layer in layers)
        {
            var rgba = layer.Color;
            SetColor(gl, rgba.R / 255f, rgba.G / 255f, rgba.B / 255f, rgba.A / 255f);
            _lineWidth?.Invoke(layer.LineWidth);

            gl.BindVertexArray(layer.Vao);
            gl.DrawArrays(ToGlPrimitive(layer.PrimitiveKind), 0, new IntPtr(layer.VertexCount));
        }
    }

    static void DeleteLayers(GlInterface gl, List<LayerGpu> layers)
    {
        foreach (var layer in layers)
        {
            if (layer.Vao != 0)
                gl.DeleteVertexArray(layer.Vao);
            gl.DeleteBuffer(layer.Vbo);
        }

        layers.Clear();
    }

    static (string Vertex, string Fragment) OpenGlEsShaders() => (
        """
        #version 300 es
        uniform mat4 uMvp;
        in vec3 aPosition;
        void main() {
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }
        """,
        """
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
        """);

    static (string Vertex, string Fragment) DesktopGlShaders() => (
        """
        #version 330 core
        uniform mat4 uMvp;
        layout(location = 0) in vec3 aPosition;
        void main() {
            gl_Position = uMvp * vec4(aPosition, 1.0);
        }
        """,
        """
        #version 330 core
        uniform float uColorR;
        uniform float uColorG;
        uniform float uColorB;
        uniform float uColorA;
        out vec4 fragColor;
        void main() {
            fragColor = vec4(uColorR, uColorG, uColorB, uColorA);
        }
        """);
}
