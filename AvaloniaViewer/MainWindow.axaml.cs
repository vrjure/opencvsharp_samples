using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OpenCvSharp;
using AvaPoint = Avalonia.Point;
using AvaRect = Avalonia.Rect;

namespace AvaloniaViewer;

public partial class MainWindow : Avalonia.Controls.Window
{
    private readonly WindowNotificationManager _notificationManager;
    public MainWindow()
    {
        InitializeComponent();

        _notificationManager = new WindowNotificationManager(this)
        {
            MaxItems = 3
        };
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count <= 0) return;
        var selectedItem = e.AddedItems[0] as ListBoxItem;
        if (selectedItem == null) return;
        UserControl? view = selectedItem.Tag?.ToString() switch
        {
            "1" => new CannyEdgeDetection(),
            "2" => new VideoCapture(_notificationManager),
            "3" => new FaceDetectYNView(),
            "4" => new FaceRecognizerView(),
            "5" => new ImageProcessPipelineView(),
            _ => null
        };
        PageContent.Content = view;
    }
}
