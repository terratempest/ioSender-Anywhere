using System.Xml.Serialization;
using CNC.Core;

namespace CNC.Converters;

[XmlRoot(ElementName = "ConversionParameters")]
public class JobParametersViewModel : ViewModelBase
{
    public enum ToolType
    {
        Drill = 0,
        Endmill,
        VBit
    }

    public struct Tool
    {
        public int Id;
        public ToolType Type;
        public double Diameter;
    }

    double _zRapids = 1d, _zHome = 25d, _zMin = -1.8d, _zSafe = 1d;
    double _rpm = 5000, _toolDiameter = 3d, _feedRate = 300d, _plungeRate = 100d;
    double _xScale = 1d, _yScale = 1d;
    bool _enableTool;

    public string Profile { get; set; } = "Default";
    [XmlIgnore]
    public List<Tool> ToolBox { get; } = [];
    public double ZHome { get => _zHome; set { _zHome = value; OnPropertyChanged(); } }
    public double ZRapids { get => _zRapids; set { _zRapids = value; OnPropertyChanged(); } }
    public double ZSafe { get => _zSafe; set { _zSafe = value; OnPropertyChanged(); } }
    public double ZMin { get => _zMin; set { _zMin = value; OnPropertyChanged(); } }
    public double RPM { get => _rpm; set { _rpm = value; OnPropertyChanged(); } }
    public double ToolDiameter { get => _toolDiameter; set { _toolDiameter = value; OnPropertyChanged(); } }
    public double FeedRate { get => _feedRate; set { _feedRate = value; OnPropertyChanged(); } }
    public double PlungeRate { get => _plungeRate; set { _plungeRate = value; OnPropertyChanged(); } }
    public double ScaleX { get => _xScale; set { _xScale = value; OnPropertyChanged(); } }
    public double ScaleY { get => _yScale; set { _yScale = value; OnPropertyChanged(); } }
    public bool EnableToolSelection { get => _enableTool; set { _enableTool = value; OnPropertyChanged(); } }
}
