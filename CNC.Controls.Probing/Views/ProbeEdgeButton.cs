using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CNC.Controls.Probing;

public sealed class ProbeEdgeButton : RadioButton
{
    public static readonly StyledProperty<Edge> EdgeProperty =
        AvaloniaProperty.Register<ProbeEdgeButton, Edge>(nameof(Edge));

    public static readonly StyledProperty<Edge> SelectedEdgeProperty =
        AvaloniaProperty.Register<ProbeEdgeButton, Edge>(nameof(SelectedEdge), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> ImageUriProperty =
        AvaloniaProperty.Register<ProbeEdgeButton, string>(nameof(ImageUri), string.Empty);

    public static readonly StyledProperty<double> ImageWidthProperty =
        AvaloniaProperty.Register<ProbeEdgeButton, double>(nameof(ImageWidth), 56d);

    public static readonly StyledProperty<double> ImageHeightProperty =
        AvaloniaProperty.Register<ProbeEdgeButton, double>(nameof(ImageHeight), 56d);

    public static readonly StyledProperty<Thickness> MarkerMarginProperty =
        AvaloniaProperty.Register<ProbeEdgeButton, Thickness>(nameof(MarkerMargin));

    public static readonly StyledProperty<bool> ProbeZProperty =
        AvaloniaProperty.Register<ProbeEdgeButton, bool>(nameof(ProbeZ));

    readonly Image _image = new()
    {
        Width = 56,
        Height = 56
    };

    readonly Rectangle _marker = new()
    {
        Width = 8,
        Height = 8,
        Fill = Brushes.Red,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    Bitmap? _bitmap;

    public ProbeEdgeButton()
    {
        Content = new Grid
        {
            Children =
            {
                _image,
                _marker
            }
        };

        UpdateChecked();
        UpdateMarker();
    }

    public Edge Edge
    {
        get => GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    public Edge SelectedEdge
    {
        get => GetValue(SelectedEdgeProperty);
        set => SetValue(SelectedEdgeProperty, value);
    }

    public string ImageUri
    {
        get => GetValue(ImageUriProperty);
        set => SetValue(ImageUriProperty, value);
    }

    public double ImageWidth
    {
        get => GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public double ImageHeight
    {
        get => GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    public Thickness MarkerMargin
    {
        get => GetValue(MarkerMarginProperty);
        set => SetValue(MarkerMarginProperty, value);
    }

    public bool ProbeZ
    {
        get => GetValue(ProbeZProperty);
        set => SetValue(ProbeZProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == EdgeProperty || change.Property == SelectedEdgeProperty)
        {
            UpdateChecked();
            UpdateMarker();
        }
        else if (change.Property == IsCheckedProperty && IsChecked == true)
        {
            SelectedEdge = Edge;
        }
        else if (change.Property == ImageUriProperty)
        {
            UpdateImage();
        }
        else if (change.Property == ImageWidthProperty)
        {
            _image.Width = ImageWidth;
        }
        else if (change.Property == ImageHeightProperty)
        {
            _image.Height = ImageHeight;
        }
        else if (change.Property == MarkerMarginProperty)
        {
            _marker.Margin = MarkerMargin;
        }
        else if (change.Property == ProbeZProperty)
        {
            UpdateMarker();
        }
    }

    void UpdateChecked() => SetCurrentValue(IsCheckedProperty, SelectedEdge == Edge);

    void UpdateMarker() => _marker.IsVisible = ProbeZ && SelectedEdge == Edge;

    void UpdateImage()
    {
        _bitmap?.Dispose();
        _bitmap = string.IsNullOrWhiteSpace(ImageUri)
            ? null
            : new Bitmap(AssetLoader.Open(new Uri(ImageUri)));

        _image.Source = _bitmap;
    }
}
