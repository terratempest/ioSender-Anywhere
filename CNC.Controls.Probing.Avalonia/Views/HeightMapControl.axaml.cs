using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;
using CNC.GCodeViewer.Avalonia;

namespace CNC.Controls.Probing;

public partial class HeightMapControl : UserControl, IProbeTab
{
    static readonly HashSet<string> PreviewParameterProperties =
    [
        nameof(HeightMapViewModel.MinX),
        nameof(HeightMapViewModel.MaxX),
        nameof(HeightMapViewModel.MinY),
        nameof(HeightMapViewModel.MaxY),
        nameof(HeightMapViewModel.Width),
        nameof(HeightMapViewModel.Height),
        nameof(HeightMapViewModel.GridSizeX),
        nameof(HeightMapViewModel.GridSizeY),
        nameof(HeightMapViewModel.GridSizeLockXY)
    ];

    HeightMapViewModel? _heightMapVm;
    HeightMap? _subscribedMap;

    int _x;
    int _y;
    Position _origin = new();

    const string Instructions =
        "A rapid motion to the map origin including any probe offset will be performed before probing starts.\n" +
        "Ensure the initial Z-position is clear of any obstacles encountered during probing.";

    public HeightMapControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookHeightMapViewModel();
        AttachedToVisualTree += (_, _) => HookHeightMapViewModel();
        DetachedFromVisualTree += (_, _) => UnhookHeightMapViewModel();
    }

    public ProbingType ProbingType => ProbingType.HeightMap;

    public void Activate(bool activate)
    {
        if (activate && DataContext is ProbingViewModel probing)
            probing.Instructions = Instructions;
    }

    public void Start(bool preview = false)
    {
        _ = preview;
        if (DataContext is not ProbingViewModel probing)
            return;

        if (!probing.ValidateInput(true))
            return;

        var startpos = new Position(
            probing.HeightMap.MinX - probing.ProbeOffsetX,
            probing.HeightMap.MinY - probing.ProbeOffsetY,
            0d);

        if ((Math.Abs(startpos.X - probing.Grbl!.Position.X) > 0.01d ||
             Math.Abs(startpos.Y - probing.Grbl.Position.Y) > 0.01d) &&
            !GrblUi.AskYesNo(
                string.Format(ProbingStrings.AreaOriginConfirm,
                    startpos.X.ToInvariantString(probing.Grbl.Format),
                    startpos.Y.ToInvariantString(probing.Grbl.Format)),
                "ioSender"))
            return;

        _origin = new Position(probing.Grbl.MachinePosition, probing.Grbl.UnitFactor);

        if (!probing.WaitForIdle(string.Format("G90G0X{0}Y{1}",
                startpos.X.ToInvariantString(probing.Grbl.Format),
                startpos.Y.ToInvariantString(probing.Grbl.Format))))
            return;

        if (!probing.VerifyProbe())
            return;

        if (!probing.Program.Init())
            return;

        probing.Message = string.Empty;
        probing.HeightMap.HasHeightMap = false;

        try
        {
            var map = new HeightMap(
                probing.HeightMap.GridSizeX,
                probing.HeightMap.GridSizeY,
                new Vector2(probing.HeightMap.MinX, probing.HeightMap.MinY),
                new Vector2(probing.HeightMap.MaxX, probing.HeightMap.MaxY));
            probing.HeightMap.Map = map;
        }
        catch (Exception ex)
        {
            probing.Message = ex.Message;
            return;
        }

        var heightMap = probing.HeightMap.Map;
        if (heightMap == null)
            return;

        probing.HeightMap.GridSizeLockXY = heightMap.GridX == heightMap.GridY;
        probing.HeightMap.GridSizeX = heightMap.GridX;
        probing.HeightMap.GridSizeY = heightMap.GridY;

        probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

        var point = 0;
        var points = heightMap.TotalPoints;
        var dir = 1d;

        for (_x = 0; _x < heightMap.SizeX; _x++)
        {
            for (_y = 0; _y < heightMap.SizeY; _y++)
            {
                probing.Program.AddMessage(string.Format(ProbingStrings.ProbingPointOf, ++point, points));

                if (probing.HeightMap.AddPause && (_x > 0 || _y > 0))
                    probing.Program.AddPause();

                probing.Program.AddProbingAction(AxisFlags.Z, true);
                probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);

                if (_y < heightMap.SizeY - 1)
                    probing.Program.AddRapid(string.Format("Y{0}",
                        (heightMap.GridY * dir).ToInvariantString(probing.Grbl.Format)));
            }

            if (_x < heightMap.SizeX - 1)
                probing.Program.AddRapid(string.Format("X{0}",
                    heightMap.GridX.ToInvariantString(probing.Grbl.Format)));

            dir *= -1d;
        }

        probing.Program.Execute(true);
        OnCompleted();
    }

    public void Stop()
    {
        if (DataContext is ProbingViewModel probing)
            probing.Program.Cancel();
    }

    void OnCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        var ok = probing.IsSuccess && probing.HeightMap.Map != null &&
                 probing.Positions.Count == probing.HeightMap.Map.TotalPoints;

        if (ok)
        {
            var activeGrbl = probing.Grbl!;
            var heightMap = probing.HeightMap.Map!;
            var z0 = probing.Positions[0].Z;
            var zMin = 0d;
            var zMax = 0d;
            var i = 0;

            for (_x = 0; _x < heightMap.SizeX; _x++)
            {
                for (_y = 0; _y < heightMap.SizeY; _y++)
                {
                    var zDelta = probing.Positions[i++].Z - z0;
                    zMin = Math.Min(zMin, zDelta);
                    zMax = Math.Max(zMax, zDelta);
                    heightMap.AddPoint(_x, _y, Math.Round(zDelta, activeGrbl.Precision));
                }

                if (++_x < heightMap.SizeX)
                {
                    for (_y = heightMap.SizeY - 1; _y >= 0; _y--)
                    {
                        var zDelta = probing.Positions[i++].Z - z0;
                        zMin = Math.Min(zMin, zDelta);
                        zMax = Math.Max(zMax, zDelta);
                        heightMap.AddPoint(_x, _y, Math.Round(zDelta, activeGrbl.Precision));
                    }
                }
            }

            probing.HeightMap.HasHeightMap = true;
            probing.HeightMap.Preview = heightMap.BuildPreview();
            UpdatePreview3D();

            if (probing.HeightMap.SetToolOffset &&
                (ok = probing.Program.ProbeZ(-probing.ProbeOffsetX, -probing.ProbeOffsetY)))
            {
                heightMap.ZOffset = Math.Round(z0 - probing.Positions[0].Z, activeGrbl.Precision);

                if (probing.CoordinateMode == ProbingViewModel.CoordMode.G10)
                {
                    activeGrbl.ExecuteCommand(string.Format("G10L2P{0}Z{1}", probing.CoordinateSystem,
                        (probing.Positions[0].Z - activeGrbl.ToolOffset.Z).ToInvariantString()));
                }
                else if ((ok = probing.GotoMachinePosition(probing.Positions[0], AxisFlags.Z)))
                {
                    activeGrbl.ExecuteCommand("G92Z0");
                    if (!activeGrbl.IsParserStateLive)
                        activeGrbl.ExecuteCommand("$G");
                }
            }

            probing.GotoMachinePosition(_origin, AxisFlags.Z);
            probing.GotoMachinePosition(_origin, AxisFlags.X | AxisFlags.Y);

            if (ok)
            {
                probing.Program.End(string.Format(ProbingStrings.HeightMapCompleted,
                    zMin.ToInvariantString(activeGrbl.Format),
                    zMax.ToInvariantString(activeGrbl.Format)));
            }
        }

        if (!ok)
            probing.Program.End(ProbingStrings.ProbingFailed,
                probing.HeightMap.Map == null || probing.Positions.Count != probing.HeightMap.Map.TotalPoints);

        probing.Program.OnCompleted?.Invoke(ok);
    }

    public void Load(string fileName)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        probing.HeightMap.HasHeightMap = false;
        var map = HeightMap.Load(fileName);
        probing.HeightMap.Map = map;
        probing.HeightMap.GridSizeLockXY = map.GridX == map.GridY;
        probing.HeightMap.GridSizeX = map.GridX;
        probing.HeightMap.GridSizeY = map.GridY;
        probing.HeightMap.MinX = map.Min.X;
        probing.HeightMap.MinY = map.Min.Y;
        probing.HeightMap.MaxX = map.Max.X;
        probing.HeightMap.MaxY = map.Max.Y;
        probing.HeightMap.HasHeightMap = true;
        probing.HeightMap.Preview = map.BuildPreview();
        UpdatePreview3D();
    }

    void OnLimitsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        var limits = new ProgramLimits(probing.Grbl!.ProgramLimits, probing.Grbl.UnitFactor);
        probing.HeightMap.MinX = limits.MinX;
        probing.HeightMap.MinY = limits.MinY;
        probing.HeightMap.MaxX = limits.MaxX;
        probing.HeightMap.MaxY = limits.MaxY;
        probing.HeightMap.RefreshPreview();
        UpdatePreview3D();
    }

    void HookHeightMapViewModel()
    {
        UnhookHeightMapViewModel();
        if (DataContext is not ProbingViewModel probing)
            return;

        _heightMapVm = probing.HeightMap;
        _heightMapVm.PropertyChanged += OnHeightMapPropertyChanged;
        SubscribeMap(_heightMapVm.Map);
        UpdatePreview3D();
    }

    void UnhookHeightMapViewModel()
    {
        if (_heightMapVm != null)
            _heightMapVm.PropertyChanged -= OnHeightMapPropertyChanged;
        _heightMapVm = null;
        SubscribeMap(null);
    }

    void SubscribeMap(HeightMap? map)
    {
        if (_subscribedMap != null)
            _subscribedMap.MapUpdated -= OnMapUpdated;
        _subscribedMap = map;
        if (_subscribedMap != null)
            _subscribedMap.MapUpdated += OnMapUpdated;
    }

    void OnHeightMapPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_heightMapVm == null || string.IsNullOrEmpty(e.PropertyName))
            return;

        if (e.PropertyName == nameof(HeightMapViewModel.Map))
        {
            SubscribeMap(_heightMapVm.Map);
            UpdatePreview3D();
            return;
        }

        if (e.PropertyName is nameof(HeightMapViewModel.Preview) or nameof(HeightMapViewModel.HasHeightMap))
        {
            UpdatePreview3D();
            return;
        }

        if (!PreviewParameterProperties.Contains(e.PropertyName))
            return;

        if (!_heightMapVm.HasHeightMap)
            _heightMapVm.RefreshPreview();
        UpdatePreview3D();
    }

    void OnMapUpdated() => UpdatePreview3D();

    void UpdatePreview3D()
    {
        if (_heightMapVm == null)
        {
            Preview3D.ClearScene();
            return;
        }

        var preview = _heightMapVm.Preview;
        HeightMapSurfaceData? surface = null;
        if (_heightMapVm.Map is { } map && HasProbedCells(map))
            surface = ToSurfaceData(map);

        Preview3D.Render(
            preview.Boundary,
            preview.GridPoints,
            preview.SizeX,
            preview.SizeY,
            surface);
    }

    static bool HasProbedCells(HeightMap map)
    {
        for (var x = 0; x < map.SizeX; x++)
        {
            for (var y = 0; y < map.SizeY; y++)
            {
                if (map.Points[x, y].HasValue)
                    return true;
            }
        }

        return false;
    }

    static HeightMapSurfaceData ToSurfaceData(HeightMap map) =>
        new()
        {
            SizeX = map.SizeX,
            SizeY = map.SizeY,
            MinX = map.Min.X,
            MinY = map.Min.Y,
            DeltaX = map.Delta.X,
            DeltaY = map.Delta.Y,
            MinHeight = map.MinHeight,
            MaxHeight = map.MaxHeight,
            Points = map.Points
        };

    async void OnLoadClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load heightmap",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Heightmap") { Patterns = ["*.map"] },
                new FilePickerFileType("All") { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            Load(path);
    }

    async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProbingViewModel probing || probing.HeightMap.Map == null)
            return;

        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save heightmap",
            DefaultExtension = "map",
            FileTypeChoices = [new FilePickerFileType("Heightmap") { Patterns = ["*.map"] }]
        });

        if (file?.TryGetLocalPath() is { } path)
            probing.HeightMap.Map.Save(path);
    }

    void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProbingViewModel probing)
            probing.TryApplyHeightMapToGCode();
    }

    void OnProbeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProbingViewModel probing)
            probing.IsPaused = false;
    }

    void OnStartClick(object? sender, RoutedEventArgs e) => Start();

    void OnStopClick(object? sender, RoutedEventArgs e) => Stop();
}
