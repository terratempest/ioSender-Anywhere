using System.Data;
using System.Net.Sockets;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Controls.Avalonia.Views;

public partial class SDCardView : UserControl
{
    DataRow? _currentFile;

    public static readonly StyledProperty<bool> RewindProperty =
        AvaloniaProperty.Register<SDCardView, bool>(nameof(Rewind));

    public static readonly StyledProperty<bool> CanRewindProperty =
        AvaloniaProperty.Register<SDCardView, bool>(nameof(CanRewind));

    public static readonly StyledProperty<bool> ViewAllProperty =
        AvaloniaProperty.Register<SDCardView, bool>(nameof(ViewAll));

    public static readonly StyledProperty<bool> CanViewAllProperty =
        AvaloniaProperty.Register<SDCardView, bool>(nameof(CanViewAll));

    public static readonly StyledProperty<bool> CanUploadProperty =
        AvaloniaProperty.Register<SDCardView, bool>(nameof(CanUpload));

    public static readonly StyledProperty<bool> CanDeleteProperty =
        AvaloniaProperty.Register<SDCardView, bool>(nameof(CanDelete));

    public SDCardView()
    {
        InitializeComponent();
        ctxMenu.DataContext = this;
        AttachedToVisualTree += (_, _) => dgrSDCard.ItemsSource = GrblSDCard.Files;
    }

    public event Action<string, bool>? FileSelected;

    public bool Rewind { get => GetValue(RewindProperty); set => SetValue(RewindProperty, value); }
    public bool CanRewind { get => GetValue(CanRewindProperty); set => SetValue(CanRewindProperty, value); }
    public bool ViewAll { get => GetValue(ViewAllProperty); set => SetValue(ViewAllProperty, value); }
    public bool CanViewAll { get => GetValue(CanViewAllProperty); set => SetValue(CanViewAllProperty, value); }
    public bool CanUpload { get => GetValue(CanUploadProperty); set => SetValue(CanUploadProperty, value); }
    public bool CanDelete { get => GetValue(CanDeleteProperty); set => SetValue(CanDeleteProperty, value); }

    public void Activate(bool activate)
    {
        if (DataContext is not GrblViewModel model)
            return;

        if (activate)
        {
            CanUpload = GrblInfo.UploadProtocol != string.Empty && model.SDCardMountStatus != SDState.Undetected;
            CanDelete = GrblInfo.Build >= 20210421;
            CanViewAll = GrblInfo.Build >= 20230312;
            CanRewind = GrblInfo.IsGrblHAL;

            if (Comms.com is not { IsOpen: true })
            {
                GrblSDCard.Clear();
                model.Message = "No connection.";
                return;
            }

            if (GrblInfo.HasSDCard && model.SDCardMountStatus == SDState.Undetected)
            {
                GrblSDCard.Clear();
                model.Message = "No card mounted.";
            }
            else
                GrblSDCard.Load(model, ViewAll);
        }
        else
            model.Message = string.Empty;
    }

    void dgrSDCard_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _currentFile = e.AddedItems.Count == 1 && e.AddedItems[0] is DataRowView drv
            ? drv.Row
            : null;
    }

    void dgrSDCard_DoubleTapped(object? sender, RoutedEventArgs e) => RunFile();

    static void AddBlock(string data) => GCodeFileService.Instance.AddBlock(data);

    static bool IsMacro(string filename) => filename.EndsWith(".macro", StringComparison.OrdinalIgnoreCase);

    void DownloadRun_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFile == null || DataContext is not GrblViewModel model || Comms.com is not { } comms)
            return;

        var name = (string)_currentFile["Name"]!;
        if ((int)_currentFile["Size"]! <= 0 || IsMacro(name) ||
            !GrblUi.AskYesNo($"Download and run {name}?", "ioSender"))
            return;

        comms.PurgeQueue();
        model.SuspendProcessing = true;
        model.Message = $"Downloading {name}...";
        GCodeFileService.Instance.AddBlock(name, CNC.Core.Action.New);

        bool? res = null;
        var cancellationToken = new CancellationToken();
        new Thread(() =>
        {
            res = WaitFor.AckResponse<string>(
                cancellationToken,
                AddBlock,
                a => model.OnResponseReceived += a,
                a => model.OnResponseReceived = (Action<string>)Delegate.Remove(model.OnResponseReceived, a)!,
                400, () => comms.WriteCommand(GrblConstants.CMD_SDCARD_DUMP + name));
        }).Start();

        while (res == null)
            EventUtils.DoEvents();

        model.SuspendProcessing = false;
        GCodeFileService.Instance.AddBlock(string.Empty, CNC.Core.Action.End);
        model.Message = string.Empty;

        if (Rewind)
            comms.WriteCommand(GrblConstants.CMD_SDCARD_REWIND);

        FileSelected?.Invoke("SDCard:" + name, Rewind);
        comms.WriteCommand(GrblConstants.CMD_SDCARD_RUN + name);
        Rewind = false;
    }

    async void Upload_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage)
            return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Upload to SD card",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("G-code") { Patterns = ["*.nc", "*.ngc", "*.gcode", "*.tap", "*.cnc", "*.txt", "*.macro"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            return;

        var ok = false;
        model.Message = "Uploading...";

        if (GrblInfo.UploadProtocol == "FTP")
        {
            var comms = Comms.com;
            if (GrblInfo.IpAddress == string.Empty)
                model.Message = "No connection.";
            else if (comms == null)
                model.Message = "No connection.";
            else
            {
                model.Message = "Uploading...";
                if (GrblInfo.Build > 20260308)
                {
                    bool? res = null;
                    var cancellationToken = new CancellationToken();
                    comms.PurgeQueue();
                    new Thread(() =>
                    {
                        res = WaitFor.AckResponse<string>(
                            cancellationToken,
                            null,
                            a => model.OnResponseReceived += a,
                            a => model.OnResponseReceived = (Action<string>)Delegate.Remove(model.OnResponseReceived, a)!,
                            300, () => comms.WriteCommand(GrblConstants.CMD_FS_PWD));
                    }).Start();
                    while (res == null)
                        EventUtils.DoEvents();
                }

                try
                {
                    var port = GrblSettings.GetInteger(grblHALSetting.FtpPort0);
                    if (port == -1)
                        port = GrblSettings.GetInteger(grblHALSetting.FtpPort1);
                    if (port == -1)
                        port = GrblSettings.GetInteger(grblHALSetting.FtpPort2);
                    var remotePath = $"{model.FsCwd}{(model.FsCwd.EndsWith('/') ? "" : "/")}{Path.GetFileName(path)}";
                    await UploadFtpFileAsync(GrblInfo.IpAddress, port == -1 ? 21 : port, remotePath, path);
                    ok = true;
                }
                catch (IOException ex)
                {
                    model.Message = ex.Message;
                }
                catch (Exception ex)
                {
                    model.Message = ex.Message;
                }
            }
        }
        else
        {
            var ymodem = new YModem();
            ymodem.DataTransferred += (size, transferred) =>
                model.Message = $"Transferred {transferred} of {size} bytes...";
            ok = ymodem.Upload(path);
        }

        if (!(GrblInfo.UploadProtocol == "FTP" && !ok))
            model.Message = ok ? "Transfer done." : "Transfer aborted.";

        GrblSDCard.Load(model, ViewAll);
    }

    static async Task UploadFtpFileAsync(string host, int port, string remotePath, string localPath)
    {
        using var control = new TcpClient();
        await control.ConnectAsync(host, port);
        await using var controlStream = control.GetStream();
        using var reader = new StreamReader(controlStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        await using var writer = new StreamWriter(controlStream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

        await ExpectFtpAsync(reader, 220);
        var userResponse = await SendFtpAsync(writer, reader, "USER grblHAL", 331, 230);
        if (userResponse.Code == 331)
            await SendFtpAsync(writer, reader, "PASS grblHAL", 230);
        await SendFtpAsync(writer, reader, "TYPE I", 200);

        using var data = await OpenPassiveDataConnectionAsync(host, writer, reader);
        await writer.WriteLineAsync("STOR " + remotePath);
        await ExpectFtpAsync(reader, 150, 125);
        await using (var file = File.OpenRead(localPath))
            await file.CopyToAsync(data.GetStream());
        data.Close();
        await ExpectFtpAsync(reader, 226, 250);
        await SendFtpAsync(writer, reader, "QUIT", 221);
    }

    static async Task<TcpClient> OpenPassiveDataConnectionAsync(string host, StreamWriter writer, StreamReader reader)
    {
        await writer.WriteLineAsync("EPSV");
        var response = await ReadFtpResponseAsync(reader);
        if (response.Code == 229)
        {
            var start = response.Message.LastIndexOf("(|||", StringComparison.Ordinal);
            var end = response.Message.LastIndexOf("|)", StringComparison.Ordinal);
            if (start >= 0 && end > start && int.TryParse(response.Message[(start + 4)..end], out var epsvPort))
            {
                var epsvClient = new TcpClient();
                await epsvClient.ConnectAsync(host, epsvPort);
                return epsvClient;
            }
        }

        await writer.WriteLineAsync("PASV");
        response = await ExpectFtpAsync(reader, 227);
        var open = response.Message.IndexOf('(');
        var close = response.Message.IndexOf(')', open + 1);
        if (open < 0 || close <= open)
            throw new IOException("FTP server returned an invalid passive endpoint.");

        var parts = response.Message[(open + 1)..close].Split(',');
        if (parts.Length != 6 ||
            !int.TryParse(parts[4], out var p1) ||
            !int.TryParse(parts[5], out var p2))
            throw new IOException("FTP server returned an invalid passive port.");

        var pasvHost = string.Join(".", parts.Take(4));
        var client = new TcpClient();
        await client.ConnectAsync(pasvHost, (p1 * 256) + p2);
        return client;
    }

    static async Task<(int Code, string Message)> SendFtpAsync(StreamWriter writer, StreamReader reader, string command, params int[] expectedCodes)
    {
        await writer.WriteLineAsync(command);
        return await ExpectFtpAsync(reader, expectedCodes);
    }

    static async Task<(int Code, string Message)> ExpectFtpAsync(StreamReader reader, params int[] expectedCodes)
    {
        var response = await ReadFtpResponseAsync(reader);
        if (!expectedCodes.Contains(response.Code))
            throw new IOException(response.Message);
        return response;
    }

    static async Task<(int Code, string Message)> ReadFtpResponseAsync(StreamReader reader)
    {
        var line = await reader.ReadLineAsync() ?? throw new IOException("FTP server closed the connection.");
        if (line.Length < 3 || !int.TryParse(line[..3], out var code))
            throw new IOException(line);

        if (line.Length > 3 && line[3] == '-')
        {
            string? next;
            do
            {
                next = await reader.ReadLineAsync();
                if (next == null)
                    throw new IOException("FTP server closed the connection.");
                line += Environment.NewLine + next;
            } while (!(next.Length >= 4 && next.StartsWith(code.ToString(), StringComparison.Ordinal) && next[3] == ' '));
        }

        return (code, line);
    }

    void Run_Click(object? sender, RoutedEventArgs e) => RunFile();

    void ViewAll_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GrblViewModel model)
            GrblSDCard.Load(model, ViewAll);
    }

    void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentFile == null || DataContext is not GrblViewModel model)
            return;

        var name = (string)_currentFile["Name"]!;
        if (GrblUi.AskYesNo($"Delete {name}?", "ioSender") &&
            Grbl.WaitForResponse(GrblConstants.CMD_SDCARD_UNLINK + name))
            GrblSDCard.Load(model, ViewAll);
    }

    void RunFile()
    {
        if (_currentFile == null || DataContext is not GrblViewModel model || Comms.com is not { } comms)
            return;

        model.Message = string.Empty;
        var name = (string)_currentFile["Name"]!;

        if ((bool)_currentFile["Invalid"]!)
        {
            GrblUi.ShowError($"File: {name}\r\n!,?,~ and SPACE is not supported in filenames, please rename.", "ioSender");
            return;
        }

        if ((int)_currentFile["Size"]! == -1)
        {
            if (Grbl.WaitForResponse(GrblConstants.CMD_SDCARD_RUN + name))
                GrblSDCard.Load(model, ViewAll);
            return;
        }

        if (GrblInfo.ExpressionsSupported && IsMacro(name))
        {
            var filename = name.ToLowerInvariant();
            filename = filename[..filename.LastIndexOf(".macro", StringComparison.Ordinal)];
            var pos = filename.LastIndexOf('p');
            if (pos >= 0 && int.TryParse(filename[(pos + 1)..], out var macro) && macro >= 100 &&
                GrblUi.AskYesNo($"Run macro {macro} with no parameters?", "ioSender"))
                comms.WriteCommand("G65P" + macro);
            return;
        }

        if (Rewind)
            comms.WriteCommand(GrblConstants.CMD_SDCARD_REWIND);

        FileSelected?.Invoke("SDCard:" + name, Rewind);
        comms.WriteCommand(GrblConstants.CMD_SDCARD_RUN + name);
        Rewind = false;
    }
}
