using Avalonia.Controls;
using CNC.App.Workspace;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.Lathe;
using CNC.Controls.Probing;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;
using CNC.GCodeViewer.Avalonia.Views;
using ioSender.Services;
using ioSender.ViewModels;

namespace ioSender.Workspace.Editors;

/// <summary>Creates workspace editor controls (shared by workspace host and quick-access sidebar).</summary>
public sealed class WorkspaceEditorControlFactory
{
    readonly object _grblContext;
    readonly object? _appConfigContext;
    readonly AppSession _session;
    readonly GCodeViewerSession _viewerSession;

    public WorkspaceEditorControlFactory(
        object grblContext,
        object? appConfigContext = null,
        AppSession? session = null,
        ProgramService? programService = null)
    {
        _session = session ?? AppHostContext.Session;
        _grblContext = WorkspaceEditorFactory.ResolveGrblContext(grblContext);
        _appConfigContext = appConfigContext ?? _session.AppConfig.Base;
        var program = programService ?? _session.Program;
        _viewerSession = new GCodeViewerSession(
            _session.AppConfig,
            (GrblViewModel)_grblContext,
            () => program.Tokens);
    }

    public Control Create(WorkspaceEditorId id)
    {
        var control = CreateCore(id);
        var desc = WorkspaceEditorCatalog.Get(id);
        if (desc.RequiresGrblDataContext)
            control.DataContext = _grblContext;
        return control;
    }

    Control CreateCore(WorkspaceEditorId id) => id switch
    {
        WorkspaceEditorId.Program => new GCodeListControl(_session.Program, _session.MachineCommands)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Viewer3D => new RenderControl
        {
            Session = _viewerSession,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Console => new ConsoleControl(),
        WorkspaceEditorId.Mdi => new MDIControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.MdiTouch => new MDITouchControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Dro => new DROControl(),
        WorkspaceEditorId.ProgramLimits => new LimitsControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Signals => new SignalsControl(),
        WorkspaceEditorId.Status => new StatusControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        },
        WorkspaceEditorId.Jog => new JogControl(_session.AppConfig) { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch },
        WorkspaceEditorId.Outline => new OutlineControl(_session.AppConfig),
        WorkspaceEditorId.Goto => new GotoControl(),
        WorkspaceEditorId.WorkParams => new WorkParametersControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Spindle => new SpindleControl(_session.MachineCommands)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        },
        WorkspaceEditorId.SpindleTouch => new SpindleControlTouch(_session.MachineCommands)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Coolant => new CoolantControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Feed => new FeedControl(_session.MachineCommands)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        },
        WorkspaceEditorId.FeedTouch => new FeedControlTouch(_session.MachineCommands)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.JobBar => new JobBarEditorControl(_session.AppConfig.Base, _session.MachineCommands),
        WorkspaceEditorId.Macros => new MacroExecuteControl(_session.AppConfig)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Keyboard => new CNC.Controls.Avalonia.Controls.PopupKeyboardControl
        {
            Layout = CNC.Controls.Avalonia.Controls.PopupKeyboardLayout.Regular,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.NumberPad => new CNC.Controls.Avalonia.Controls.PopupKeyboardControl
        {
            Layout = CNC.Controls.Avalonia.Controls.PopupKeyboardLayout.Numeric,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Probing => new ProbingView(_session.AppConfig.Base),
        WorkspaceEditorId.SdCard => new SDCardView(),
        WorkspaceEditorId.Lathe => new LatheWizardsView(),
        WorkspaceEditorId.Offsets => new OffsetView(),
        WorkspaceEditorId.Tools => new ToolView(),
        WorkspaceEditorId.GrblConfig => new GrblConfigView(_session.AppConfig.Base),
        WorkspaceEditorId.AppConfig => CreateAppConfig(),
        WorkspaceEditorId.TabGroup => new TextBlock { Text = "Tab Group" },
        _ => new TextBlock { Text = id.ToString() },
    };

    Control CreateAppConfig() =>
        new AppConfigView(_session.AppConfig, _session.GameController) { DataContext = _appConfigContext };
}
