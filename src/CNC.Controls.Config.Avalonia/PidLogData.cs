using System.ComponentModel;
using System.Data;
using System.Globalization;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Config;

public static class GrblPidData
{
    public static DataTable Data { get; } = CreateTable();

    static DataTable CreateTable()
    {
        var table = new DataTable("PIDData");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Target", typeof(double));
        table.Columns.Add("Actual", typeof(double));
        table.Columns.Add("Error", typeof(double));
        table.PrimaryKey = [table.Columns["Id"]!];
        return table;
    }

    public static void Load()
    {
        var raw = string.Empty;
        Data.Clear();

        void Process(string line)
        {
            if (line.StartsWith("[PID:", StringComparison.Ordinal))
                raw = line[..^1][5..];
        }

        if (Comms.com is not { } comms)
            return;

        comms.DataReceived += Process;
        comms.AwaitAck(((char)GrblConstants.CMD_PID_REPORT).ToString(CultureInfo.InvariantCulture));
        comms.DataReceived -= Process;

        if (string.IsNullOrEmpty(raw))
            return;

        var parts = raw.Split('|');
        if (parts.Length < 2)
            return;

        var i = 0;
        var phase = 0;
        double target = 0;
        foreach (var sample in parts[1].Split(','))
        {
            switch (phase)
            {
                case 0:
                    target = dbl.Parse(sample);
                    phase = 1;
                    break;
                case 1:
                    var actual = dbl.Parse(sample);
                    Data.Rows.Add(++i, target, actual, Math.Round(actual - target, 3));
                    phase = 0;
                    break;
            }
        }
    }
}

public sealed class PidLogViewModel : INotifyPropertyChanged
{
    readonly double[] _gridLabels = new double[4];
    readonly double[] _scaleFactors = [100d, 200d, 1000d, 2000d, 5000d, 10000d];
    int _errorScale;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ErrorScale
    {
        get => _errorScale;
        set
        {
            if (_errorScale == value)
                return;
            _errorScale = value;
            var f = _scaleFactors[_errorScale];
            GridLabel4 = 200000d / f;
            GridLabel3 = 200000d / f * .75d;
            GridLabel2 = 200000d / f * .5d;
            GridLabel1 = 200000d / f * .25d;
            OnPropertyChanged(nameof(ErrorScale));
        }
    }

    public double GridLabel4 { get => _gridLabels[3]; private set { _gridLabels[3] = value; OnPropertyChanged(nameof(GridLabel4)); } }
    public double GridLabel3 { get => _gridLabels[2]; private set { _gridLabels[2] = value; OnPropertyChanged(nameof(GridLabel3)); } }
    public double GridLabel2 { get => _gridLabels[1]; private set { _gridLabels[1] = value; OnPropertyChanged(nameof(GridLabel2)); } }
    public double GridLabel1 { get => _gridLabels[0]; private set { _gridLabels[0] = value; OnPropertyChanged(nameof(GridLabel1)); } }

    public double[] ScaleFactors => _scaleFactors;

    void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
