using System.Data;
using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

public static class GrblSDCard
{
    static readonly DataTable Data;
    static int _id;
    static GrblViewModel? _grbl;

    static GrblSDCard()
    {
        Data = new DataTable("Filelist");
        Data.Columns.Add("Id", typeof(int));
        Data.Columns.Add("Dir", typeof(string));
        Data.Columns.Add("Name", typeof(string));
        Data.Columns.Add("Size", typeof(int));
        Data.Columns.Add("Invalid", typeof(bool));
        Data.PrimaryKey = [Data.Columns["Id"]!];
    }

    public static DataView Files => Data.DefaultView;
    public static bool Loaded => Data.Rows.Count > 0;

    public static void Clear() => Data.Clear();

    public static void Load(GrblViewModel model, bool viewAll)
    {
        if (Comms.com is not { IsOpen: true })
            return;

        bool? res = null;
        var cancellationToken = new CancellationToken();
        _grbl = model;
        Data.Clear();

        if (GrblInfo.HasSDCard && model.SDCardMountStatus == SDState.Unmounted)
        {
            Comms.com.PurgeQueue();
            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    CardCheck,
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived = (Action<string>)Delegate.Remove(model.OnResponseReceived, a)!,
                    1500, () => Comms.com.WriteCommand(GrblConstants.CMD_SDCARD_MOUNT));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();
        }

        if (!GrblInfo.HasSDCard || model.SDCardMountStatus == SDState.Mounted || model.SDCardMountStatus == SDState.Detected)
        {
            Comms.com.PurgeQueue();
            _id = 0;
            res = null;
            model.Silent = true;

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    cancellationToken,
                    Process,
                    a => model.OnResponseReceived += a,
                    a => model.OnResponseReceived = (Action<string>)Delegate.Remove(model.OnResponseReceived, a)!,
                    2000, () => Comms.com.WriteCommand(viewAll ? GrblConstants.CMD_SDCARD_DIR_ALL : GrblConstants.CMD_SDCARD_DIR));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            model.Silent = false;
            Data.AcceptChanges();
        }
    }

    static void CardCheck(string data)
    {
        if (data == "ok")
            _grbl!.SDCardMountStatus = SDState.Mounted;
    }

    static void Process(string data)
    {
        if (!data.StartsWith("[FILE:"))
        {
            if (data is "error:62" or "error:64")
                _grbl!.SDCardMountStatus = SDState.Unmounted;
            return;
        }

        var filename = "";
        var filesize = 0;
        var invalid = false;
        var parameters = data.TrimEnd(']').Split('|');
        foreach (var parameter in parameters)
        {
            var valuepair = parameter.Split(':');
            switch (valuepair[0])
            {
                case "[FILE":
                    filename = valuepair[1];
                    break;
                case "SIZE":
                    filesize = int.Parse(valuepair[1]);
                    break;
                case "INVALID":
                    invalid = true;
                    break;
            }
        }

        Data.Rows.Add(_id++, "", filename, filesize, invalid);
    }
}
