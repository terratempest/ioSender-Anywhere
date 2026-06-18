using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.Utility.GCode;
using CNC.GCodeViewer.Avalonia;
using CNC.Platform.Windows;
using System.Runtime.Serialization;

namespace CNC.Platform.Tests;

public sealed class GCodeFileLoadTests
{
    [Fact]
    public void LoadFile_GeneratedLargeProgram_LoadsBlocksTokensAndBounds()
    {
        var path = CreateProgramFile(25_000);
        var job = new GCodeJob();
        var changed = false;
        job.FileChanged += filename => changed = filename == path;

        try
        {
            Assert.True(job.LoadFile(path));

            Assert.True(changed);
            Assert.Equal(25_000, job.Blocks.Count);
            Assert.NotEmpty(job.Tokens);
            Assert.True(job.BoundingBox.Min[0] <= 2d);
            Assert.Equal(25_000d, job.BoundingBox.Max[0], 6);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadFile_SuppressedFileChanged_DoesNotRaiseFromWorkerLoad()
    {
        var path = CreateProgramFile(10_000);
        var job = new GCodeJob();
        var changed = 0;
        job.FileChanged += _ => changed++;

        try
        {
            var loaded = await Task.Run(() => job.LoadFile(path, raiseFileChanged: false));

            Assert.True(loaded);
            Assert.Equal(0, changed);
            Assert.Equal(10_000, job.Blocks.Count);
            Assert.NotEmpty(job.Tokens);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_GeneratedLargeProgram_RaisesLoadEvents()
    {
        var path = CreateProgramFile(10_000);
        var loading = 0;
        var changed = 0;
        var service = GCodeFileService.Instance;

        void OnLoading() => loading++;
        void OnChanged() => changed++;

        service.ProgramLoading += OnLoading;
        service.ProgramChanged += OnChanged;

        try
        {
            await service.LoadAsync(path);

            Assert.Equal(1, loading);
            Assert.Equal(1, changed);
            Assert.Equal(10_000, service.Blocks);
            Assert.NotEmpty(service.Tokens);
        }
        finally
        {
            service.ProgramLoading -= OnLoading;
            service.ProgramChanged -= OnChanged;
            service.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProgramService_LoadAsync_UtilityGeneratedTempFile_LoadsAsPhysicalProgram()
    {
        var lines = UtilityGCodeGenerator.GenerateSurfacing(new SurfacingOptions(
            UtilityUnits.Metric,
            UtilityOrigin.LowerLeft,
            0d,
            0d,
            0d,
            4d,
            12d,
            8d,
            0d,
            1,
            0,
            50d,
            100d,
            25d,
            5d,
            12000,
            new CoolantOptions(false, false),
            SurfacingPassDirection.AlongX,
            SurfacingCutType.Both));
        var path = CreateProgramFile(lines);
        var model = new GrblViewModel { PathService = new WindowsPathService() };
        var service = new ProgramService
        {
            Model = model,
        };

        try
        {
            await service.LoadAsync(path);

            Assert.True(service.IsLoaded);
            Assert.True(service.Blocks > 0);
            Assert.NotEmpty(service.Tokens);
            Assert.Equal(path, model.FileName);
            Assert.True(model.IsPhysicalFileLoaded);
        }
        finally
        {
            service.Close();
            File.Delete(path);
        }
    }

    [Fact]
    public void GCodeListViewModel_InjectedProgram_DoesNotClearProgramModelOnDetach()
    {
        var service = new ProgramService();
        var appModel = new GrblViewModel { PathService = new WindowsPathService() };
        var controlModel = new GrblViewModel { PathService = new WindowsPathService() };

        try
        {
            service.Model = appModel;
            var viewModel = new GCodeListViewModel(service);

            viewModel.Model = controlModel;
            viewModel.Model = null;

            Assert.Same(appModel, service.Model);
        }
        finally
        {
            service.Close();
            service.Model = null;
        }
    }

    [Fact]
    public void FileActionViewModel_InjectedProgram_DoesNotClearProgramModelOnDetach()
    {
        var service = new ProgramService();
        var appModel = new GrblViewModel { PathService = new WindowsPathService() };
        var controlModel = new GrblViewModel { PathService = new WindowsPathService() };

        try
        {
            service.Model = appModel;
            var viewModel = new FileActionViewModel(service);

            viewModel.Model = controlModel;
            viewModel.Model = null;

            Assert.Same(appModel, service.Model);
        }
        finally
        {
            service.Close();
            service.Model = null;
        }
    }

    [Fact]
    public async Task ProgramService_LoadAsync_AfterProgramTabDetach_UpdatesMainModel()
    {
        var firstPath = CreateProgramFile(new[]
        {
            "G21 G90 F100",
            "G0 X0 Y0",
            "G1 X1"
        });
        var utilityLines = UtilityGCodeGenerator.GenerateDrilling(new DrillingOptions(
            UtilityUnits.Metric,
            5d,
            1d,
            100d,
            50d,
            12000,
            new CoolantOptions(false, false),
            [new DrillHole(1d, 2d, -1d, 0d)]));
        var utilityPath = CreateProgramFile(utilityLines);
        var model = new GrblViewModel { PathService = new WindowsPathService() };
        var service = new ProgramService
        {
            Model = model,
        };

        try
        {
            await service.LoadAsync(firstPath);
            var previousBlocks = model.Blocks;

            var programTab = new GCodeListViewModel(service)
            {
                Model = model,
            };
            programTab.Model = null;

            await service.LoadAsync(utilityPath);

            Assert.True(service.IsLoaded);
            Assert.NotEqual(previousBlocks, service.Blocks);
            Assert.Equal(service.Blocks, model.Blocks);
            Assert.Equal(utilityPath, model.FileName);
            Assert.NotEmpty(service.Tokens);
        }
        finally
        {
            service.Close();
            service.Model = null;
            File.Delete(firstPath);
            File.Delete(utilityPath);
        }
    }

    [Fact]
    public void LoadFile_MixedLinearAndArcProgram_MatchesGeneralParserBounds()
    {
        var lines = new[]
        {
            "G21 G90 G17 F100",
            "G0 X0 Y0 Z0",
            "G1 X10 Y0",
            "G2 X20 Y0 I5 J0",
            "G3 X10 Y0 I-5 J0",
            "G2 X20 Y0 R5",
            "G91",
            "G1 X1 Z1",
            "G2 X1 Y0 I0.5 J0",
            "M2"
        };
        var fastPath = CreateProgramFile(lines);
        var fallbackPath = CreateProgramFile(new[] { "G94" }.Concat(lines));
        var slow = new GCodeJob();
        var fast = new GCodeJob();

        try
        {
            Assert.True(slow.LoadFile(fallbackPath));
            Assert.True(fast.LoadFile(fastPath));

            Assert.Equal(lines.Length, fast.Blocks.Count);
            Assert.Equal(slow.BoundingBox.Min[0], fast.BoundingBox.Min[0], 6);
            Assert.Equal(slow.BoundingBox.Max[0], fast.BoundingBox.Max[0], 6);
            Assert.Equal(slow.BoundingBox.Min[1], fast.BoundingBox.Min[1], 6);
            Assert.Equal(slow.BoundingBox.Max[1], fast.BoundingBox.Max[1], 6);
            Assert.Equal(slow.BoundingBox.Min[2], fast.BoundingBox.Min[2], 6);
            Assert.Equal(slow.BoundingBox.Max[2], fast.BoundingBox.Max[2], 6);
        }
        finally
        {
            File.Delete(fastPath);
            File.Delete(fallbackPath);
        }
    }

    [Fact]
    public void LoadFile_ArcPlanes_LoadsBounds()
    {
        var lines = new[]
        {
            "G21 G90 F100",
            "G17 G0 X0 Y0 Z0",
            "G2 X10 Y0 I5 J0",
            "G18 G0 X0 Z0",
            "G2 X10 Z0 I5 K0",
            "G19 G0 Y0 Z0",
            "G2 Y10 Z0 J5 K0"
        };
        var path = CreateProgramFile(lines);
        var job = new GCodeJob();

        try
        {
            Assert.True(job.LoadFile(path));

            Assert.Equal(lines.Length, job.Blocks.Count);
            Assert.True(job.BoundingBox.Max[0] >= 10d);
            Assert.True(job.BoundingBox.Max[1] >= 10d);
            Assert.True(job.BoundingBox.Max[2] >= 0d);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_LargeArcProgram_BuildsPreviewSegments()
    {
        var lines = new List<string> { "G21 G90 G17 F100", "G0 X0 Y0" };
        for (var i = 0; i < 2_000; i++)
        {
            var x = i % 2 == 0 ? 10 : 0;
            var iOffset = i % 2 == 0 ? 5 : -5;
            lines.Add($"G2 X{x} Y0 I{iOffset} J0");
        }

        var path = CreateProgramFile(lines);
        var job = new GCodeJob();

        try
        {
            Assert.True(job.LoadFile(path));

            var preview = GCodePathBuilder.Build(job.Tokens, new Point3D());
            Assert.NotEmpty(preview.Segments.Cut);
            Assert.Equal(lines.Count, job.Blocks.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_UnsupportedSyntax_FallsBackToGeneralParser()
    {
        var path = CreateProgramFile(["G21 G90", "#1=1", "G1 X1"]);
        var job = new GCodeJob();

        try
        {
            Assert.True(job.LoadFile(path));

            Assert.Equal(2, job.Blocks.Count);
            Assert.NotEmpty(job.Tokens);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_WithOpenComms_DoesNotQueryControllerForBounds()
    {
        var path = CreateProgramFile(["G94", "G21 G90 G17 F100", "G0 X0 Y0", "G1 X10 Y5", "G2 X20 Y5 I5 J0"]);
        var previousComms = Comms.com;
        var previousModel = Grbl.GrblViewModel;
        var comms = new QueryCountingComms();

        try
        {
            Comms.com = comms;
            Grbl.GrblViewModel = (GrblViewModel)FormatterServices.GetUninitializedObject(typeof(GrblViewModel));

            var job = new GCodeJob();

            Assert.True(job.LoadFile(path));
            Assert.Equal(0, comms.WriteCommandCount);
            Assert.Equal(5, job.Blocks.Count);
            Assert.True(job.BoundingBox.Max[0] >= 20d);
        }
        finally
        {
            Comms.com = previousComms;
            Grbl.GrblViewModel = previousModel;
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFile_OpenCommsBounds_MatchOfflineBounds()
    {
        var lines = new[] { "G94", "G21 G90 G17 F100", "G0 X0 Y0 Z0", "G1 X10 Y0", "G2 X20 Y0 I5 J0", "G1 Z-1" };
        var offlinePath = CreateProgramFile(lines);
        var connectedPath = CreateProgramFile(lines);
        var previousComms = Comms.com;

        try
        {
            var offline = new GCodeJob();
            Assert.True(offline.LoadFile(offlinePath));

            Comms.com = new QueryCountingComms();
            var connected = new GCodeJob();
            Assert.True(connected.LoadFile(connectedPath));

            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(offline.BoundingBox.Min[i], connected.BoundingBox.Min[i], 6);
                Assert.Equal(offline.BoundingBox.Max[i], connected.BoundingBox.Max[i], 6);
            }
        }
        finally
        {
            Comms.com = previousComms;
            File.Delete(offlinePath);
            File.Delete(connectedPath);
        }
    }

    static string CreateProgramFile(int lineCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"iosender-load-{Guid.NewGuid():N}.nc");
        using var writer = new StreamWriter(path);
        writer.WriteLine("G21 G90 F100");
        for (var i = 2; i <= lineCount; i++)
            writer.WriteLine($"G1 X{i} Y{i % 10}");
        return path;
    }

    static string CreateProgramFile(IEnumerable<string> lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"iosender-load-{Guid.NewGuid():N}.nc");
        File.WriteAllLines(path, lines);
        return path;
    }

    sealed class QueryCountingComms : StreamComms
    {
        public bool IsOpen => true;
        public int OutCount => 0;
        public string Reply => "ok";
        public Comms.StreamType StreamType => Comms.StreamType.Serial;
        public Comms.State CommandState { get; set; } = Comms.State.ACK;
        public bool EventMode { get; set; }
        public Action<int>? ByteReceived { get; set; }
        public int WriteCommandCount { get; private set; }

        public event DataReceivedHandler? DataReceived;

        public void Close() { }
        public int ReadByte() => -1;
        public void WriteByte(byte data) { }
        public void WriteBytes(byte[] bytes, int len) { }
        public void WriteString(string data) { }

        public void WriteCommand(string command)
        {
            WriteCommandCount++;
            var handler = Grbl.GrblViewModel?.OnResponseReceived;
            if (handler == null)
                return;

            Task.Run(() =>
            {
                handler("ok");
            });
        }

        public string GetReply(string command)
        {
            WriteCommand(command);
            return "ok";
        }

        public void AwaitAck() { }
        public void AwaitAck(string command) => WriteCommand(command);
        public void AwaitResponse(string command) => WriteCommand(command);
        public void AwaitResponse() { }
        public void PurgeQueue() { }
    }
}
