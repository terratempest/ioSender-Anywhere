using System.Numerics;
using CNC.App;
using CNC.Core.Geometry;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.OpenGl;

/// <summary>Orthographic orbit camera (Z-up default; supports saved presets).</summary>
public sealed class ViewerCamera
{
    public NumericVector3 Position { get; set; }
    public NumericVector3 LookDirection { get; set; } = new(0f, 0f, -100f);
    public NumericVector3 UpDirection { get; set; } = new(0f, 1f, 0f);
    public double ViewWidth { get; set; } = 100d;

    public Matrix4x4 GetViewMatrix()
    {
        var target = Position + LookDirection;
        return Matrix4x4.CreateLookAt(Position, target, UpDirection);
    }

    public Matrix4x4 GetProjectionMatrix(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth < 1 || viewportHeight < 1)
            return Matrix4x4.Identity;

        var aspect = (float)(viewportWidth / viewportHeight);
        var halfH = (float)(ViewWidth * 0.5);
        var halfW = halfH * aspect;
        return Matrix4x4.CreateOrthographic(halfW * 2f, halfH * 2f, 0.01f, 1_000_000f);
    }

    /// <summary>Row-vector MVP matching Avalonia OpenGL samples (upload with transpose=false).</summary>
    public Matrix4x4 GetMvpMatrix(double viewportWidth, double viewportHeight) =>
        GetViewMatrix() * GetProjectionMatrix(viewportWidth, viewportHeight);

    public bool IsValid()
    {
        if (ViewWidth < 1e-6 || LookDirection.LengthSquared() < 1e-6f)
            return false;

        var look = NumericVector3.Normalize(LookDirection);
        var up = NumericVector3.Normalize(UpDirection);
        return NumericVector3.Cross(look, up).LengthSquared() > 1e-6f;
    }

    public void ResetToTopView(PathBounds bounds)
    {
        if (bounds.IsEmpty)
        {
            Position = new NumericVector3(0f, 0f, 100f);
            LookDirection = new NumericVector3(0f, 0f, -100f);
            UpDirection = new NumericVector3(0f, 1f, 0f);
            ViewWidth = 100d;
            return;
        }

        OrientTo(NumericVector3.UnitZ, bounds);
    }

    public void OrientTo(NumericVector3 faceDirection, PathBounds bounds)
    {
        if (faceDirection.LengthSquared() < 1e-8f)
            return;

        var center = bounds.IsEmpty
            ? NumericVector3.Zero
            : new NumericVector3((float)bounds.Center.X, (float)bounds.Center.Y, (float)bounds.Center.Z);
        var span = bounds.IsEmpty ? 100d : Math.Max(bounds.MaxSize, 20d);
        var distance = Math.Max(span * 2d, 100d);
        ViewWidth = Math.Max(span * 1.25d, 20d);

        var dir = NumericVector3.Normalize(faceDirection);
        Position = center + dir * (float)distance;
        LookDirection = -dir * (float)distance;
        UpDirection = ChooseUpDirection(dir);
    }

    /// <summary>0=3D/XY top, 1=XZ, 2=YZ (matches legacy ViewMode).</summary>
    public void ApplyPresetView(int viewMode, PathBounds bounds)
    {
        var center = new NumericVector3((float)bounds.Center.X, (float)bounds.Center.Y, (float)bounds.Center.Z);
        var span = Math.Max(bounds.MaxSize, 20d);
        var distance = Math.Max(span * 2d, 100d);
        ViewWidth = Math.Max(span * 1.25d, 20d);

        switch (viewMode)
        {
            case 1:
                Position = center + new NumericVector3(0f, -(float)distance, 0f);
                LookDirection = new NumericVector3(0f, (float)distance, 0f);
                UpDirection = new NumericVector3(0f, 1f, 1f);
                break;
            case 2:
                Position = center + new NumericVector3((float)distance, 0f, 0f);
                LookDirection = new NumericVector3(-(float)distance, 0f, 0f);
                UpDirection = new NumericVector3(1f, 0f, 1f);
                break;
            default:
                Position = center + new NumericVector3(0f, 0f, (float)distance);
                LookDirection = new NumericVector3(0f, 0f, -(float)distance);
                UpDirection = NumericVector3.Normalize(new NumericVector3(0f, 1f, 1f));
                break;
        }
    }

    public bool TryRestoreFromConfig(GCodeViewerConfig cfg, PathBounds bounds)
    {
        if (cfg.ViewMode < 0)
            return false;

        var ld = cfg.CameraLookDirection;
        if (ld.X * ld.X + ld.Y * ld.Y + ld.Z * ld.Z > 1e-6)
        {
            Position = new NumericVector3(
                (float)cfg.CameraPosition.X,
                (float)cfg.CameraPosition.Y,
                (float)cfg.CameraPosition.Z);
            LookDirection = new NumericVector3(
                (float)cfg.CameraLookDirection.X,
                (float)cfg.CameraLookDirection.Y,
                (float)cfg.CameraLookDirection.Z);
            UpDirection = NumericVector3.Normalize(new NumericVector3(
                (float)cfg.CameraUpDirection.X,
                (float)cfg.CameraUpDirection.Y,
                (float)cfg.CameraUpDirection.Z));
            if (!bounds.IsEmpty)
                ViewWidth = Math.Max(bounds.MaxSize * 1.25d, 20d);
            return IsValid();
        }

        if (!bounds.IsEmpty)
        {
            ApplyPresetView(cfg.ViewMode, bounds);
            return true;
        }

        return false;
    }

    public void SaveToConfig(GCodeViewerConfig cfg)
    {
        cfg.CameraPosition = new Point3D(Position.X, Position.Y, Position.Z);
        cfg.CameraLookDirection = new CNC.Core.Geometry.Vector3(LookDirection.X, LookDirection.Y, LookDirection.Z);
        cfg.CameraUpDirection = new CNC.Core.Geometry.Vector3(UpDirection.X, UpDirection.Y, UpDirection.Z);
    }

    public void PanPixels(global::Avalonia.Vector delta, double viewportWidth)
    {
        if (viewportWidth <= 0)
            return;

        var unitsPerPixel = ViewWidth / viewportWidth;
        var look = NumericVector3.Normalize(LookDirection);
        var up = NumericVector3.Normalize(UpDirection);
        var right = NumericVector3.Normalize(NumericVector3.Cross(look, up));
        var move = right * (float)(-delta.X * unitsPerPixel) + up * (float)(delta.Y * unitsPerPixel);
        Position += move;
    }

    public void RotatePixels(global::Avalonia.Vector delta)
    {
        var target = Position + LookDirection;
        var offset = Position - target;
        if (offset.LengthSquared() <= 1e-8f)
            return;

        var look = NumericVector3.Normalize(LookDirection);
        var up = NumericVector3.Normalize(UpDirection);
        var right = NumericVector3.Normalize(NumericVector3.Cross(look, up));
        var yaw = (float)(-delta.X * 0.01d);
        var pitch = (float)(-delta.Y * 0.01d);
        var yawRotation = Quaternion.CreateFromAxisAngle(new NumericVector3(0f, 0f, 1f), yaw);
        var pitchRotation = Quaternion.CreateFromAxisAngle(right, pitch);
        var rotation = Quaternion.Normalize(pitchRotation * yawRotation);

        var newOffset = NumericVector3.Transform(offset, rotation);
        var newUp = NumericVector3.Normalize(NumericVector3.Transform(up, rotation));
        Position = target + newOffset;
        LookDirection = target - Position;
        UpDirection = newUp;
    }

    public void ZoomWheel(double deltaY, global::Avalonia.Point pointer, double viewportWidth, double viewportHeight)
    {
        var scale = deltaY > 0d ? 0.88d : 1.14d;
        var oldWidth = ViewWidth;
        ViewWidth = Math.Clamp(ViewWidth * scale, 0.01d, 1_000_000d);

        if (viewportWidth > 1 && viewportHeight > 1)
        {
            var aspect = viewportWidth / viewportHeight;
            var halfH = oldWidth * 0.5;
            var halfW = halfH * aspect;
            var nx = (pointer.X / viewportWidth - 0.5) * 2.0 * halfW;
            var ny = (0.5 - pointer.Y / viewportHeight) * 2.0 * halfH;

            var look = NumericVector3.Normalize(LookDirection);
            var up = NumericVector3.Normalize(UpDirection);
            var right = NumericVector3.Normalize(NumericVector3.Cross(look, up));
            var zoomFactor = 1d - scale;
            Position += right * (float)(nx * zoomFactor) + up * (float)(ny * zoomFactor);
        }
    }

    public void ZoomExtents(PathBounds bounds)
    {
        if (!bounds.IsEmpty)
            ResetToTopView(bounds);
    }

    static NumericVector3 ChooseUpDirection(NumericVector3 faceDirection)
    {
        var worldZ = NumericVector3.UnitZ;
        if (Math.Abs(NumericVector3.Dot(faceDirection, worldZ)) < 0.9f)
            return worldZ;

        return NumericVector3.UnitY;
    }

    public double DistanceTo(NumericVector3 worldPoint)
    {
        var target = Position + LookDirection;
        return NumericVector3.Distance(Position, worldPoint);
    }
}
