using CNC.Core;

namespace CNC.Controls.Probing;

public class HeightMapViewModel : ViewModelBase
{
    bool _hasHeightMap;
    bool _canApply;
    bool _setToolOffset;
    bool _addPause;
    bool _lockGridSizeXY = true;
    double _minX;
    double _minY = 50d;
    double _maxX = 50d;
    double _maxY = 50d;
    double _gridSizeX = 5d;
    double _gridSizeY = 5d;
    HeightMap? _heightMap;
    HeightMapPreview _preview = new();

    public double MinX
    {
        get => _minX;
        set { if (value == _minX) return; _minX = value; OnPropertyChanged(); OnPropertyChanged(nameof(Width)); }
    }

    public double MaxX
    {
        get => _maxX;
        set { if (value == _maxX) return; _maxX = value; OnPropertyChanged(); OnPropertyChanged(nameof(Width)); }
    }

    public double MinY
    {
        get => _minY;
        set { if (value == _minY) return; _minY = value; OnPropertyChanged(); OnPropertyChanged(nameof(Height)); }
    }

    public double MaxY
    {
        get => _maxY;
        set { if (value == _maxY) return; _maxY = value; OnPropertyChanged(); OnPropertyChanged(nameof(Height)); }
    }

    public double Width
    {
        get => _maxX - _minX;
        set
        {
            if (Math.Abs(value - Width) < double.Epsilon)
                return;
            _maxX = value + _minX;
            OnPropertyChanged(nameof(MaxX));
        }
    }

    public double Height
    {
        get => _maxY - _minY;
        set
        {
            if (Math.Abs(value - Height) < double.Epsilon)
                return;
            _maxY = value + _minY;
            OnPropertyChanged(nameof(MaxY));
        }
    }

    public HeightMap? Map
    {
        get => _heightMap;
        set { if (value == _heightMap) return; _heightMap = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasHeightMap)); }
    }

    public bool HasHeightMap
    {
        get => _hasHeightMap && _heightMap != null;
        set { if (value == _hasHeightMap) return; _hasHeightMap = value; OnPropertyChanged(); }
    }

    public bool CanApply
    {
        get => _canApply && HasHeightMap;
        set { _canApply = value; OnPropertyChanged(); }
    }

    public bool SetToolOffset
    {
        get => _setToolOffset;
        set { _setToolOffset = value; OnPropertyChanged(); }
    }

    public bool AddPause
    {
        get => _addPause;
        set { _addPause = value; OnPropertyChanged(); }
    }

    public bool GridSizeLockXY
    {
        get => _lockGridSizeXY;
        set
        {
            _lockGridSizeXY = value;
            OnPropertyChanged();
            if (_lockGridSizeXY && Math.Abs(_gridSizeY - _gridSizeX) > double.Epsilon)
            {
                _gridSizeY = _gridSizeX;
                OnPropertyChanged(nameof(GridSizeY));
            }
        }
    }

    public double GridSizeX
    {
        get => _gridSizeX;
        set
        {
            _gridSizeX = value;
            OnPropertyChanged();
            if (_lockGridSizeXY)
            {
                _gridSizeY = value;
                OnPropertyChanged(nameof(GridSizeY));
            }
        }
    }

    public double GridSizeY
    {
        get => _gridSizeY;
        set
        {
            _gridSizeY = value;
            OnPropertyChanged();
            if (_lockGridSizeXY)
            {
                _gridSizeX = value;
                OnPropertyChanged(nameof(GridSizeX));
            }
        }
    }

    public HeightMapPreview Preview
    {
        get => _preview;
        set { _preview = value; OnPropertyChanged(); }
    }

    public void RefreshPreview() =>
        Preview = Map != null ? Map.BuildPreview() : HeightMap.BuildPreview(new Vector2(MinX, MinY), new Vector2(MaxX, MaxY), GridSizeX);
}
