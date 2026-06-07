using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class ConsoleControl : UserControl
{
    bool _isTextChangeSubscribed;
    GrblViewModel? _subscribedViewModel;

    public ConsoleControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Localize.Apply(ChkVerbose);
        Localize.Apply(ChkFilterRt);
        Localize.Apply(ChkShowAllRt);
        Localize.Apply(ChkFilterOk);
        Localize.Apply(ChkAutoscroll);
        Localize.Apply(BtnClear);

        if (!_isTextChangeSubscribed)
        {
            ConsoleText.TextChanged += OnConsoleTextChanged;
            _isTextChangeSubscribed = true;
        }

        SubscribeViewModel(DataContext as GrblViewModel);
        QueueScrollToEnd();
    }

    void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_isTextChangeSubscribed)
        {
            ConsoleText.TextChanged -= OnConsoleTextChanged;
            _isTextChangeSubscribed = false;
        }

        SubscribeViewModel(null);
    }

    void OnDataContextChanged(object? sender, EventArgs e) =>
        SubscribeViewModel(DataContext as GrblViewModel);

    void SubscribeViewModel(GrblViewModel? vm)
    {
        if (ReferenceEquals(_subscribedViewModel, vm))
            return;

        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribedViewModel = vm;

        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GrblViewModel.ResponseLogAutoscroll))
            QueueScrollToEnd();
    }

    void OnConsoleTextChanged(object? sender, TextChangedEventArgs e) =>
        QueueScrollToEnd();

    void QueueScrollToEnd()
    {
        if (DataContext is not GrblViewModel { ResponseLogAutoscroll: true })
            return;

        Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Background);
    }

    void ScrollToEnd()
    {
        if (DataContext is not GrblViewModel { ResponseLogAutoscroll: true })
            return;

        ConsoleText.CaretIndex = ConsoleText.Text?.Length ?? 0;
        ConsoleText.ClearSelection();
        ConsoleText.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault()
            ?.ScrollToEnd();
    }

    void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GrblViewModel vm)
            vm.ResponseLog.Clear();
    }
}
