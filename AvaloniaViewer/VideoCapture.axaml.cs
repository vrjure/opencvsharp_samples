using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Rendering.Composition;
using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace AvaloniaViewer;

public partial class VideoCapture : UserControl
{
    private readonly WindowNotificationManager? _notificationManager;
    private readonly OpenCvSharp.VideoCapture capture;
    private readonly CascadeClassifier cascadeClassifier;
    private VideoCaptureVisualHandler _videoVisualHandler;
    public VideoCapture(WindowNotificationManager? notificationManager = null)
    {
        InitializeComponent();
        _notificationManager = notificationManager;
        capture = new OpenCvSharp.VideoCapture();
        cascadeClassifier = new CascadeClassifier("haarcascade_frontalface_default.xml");
        _videoVisualHandler = new VideoCaptureVisualHandler(capture, cascadeClassifier);
        RTR.VisualHandler = _videoVisualHandler;
        Loaded += ViewLoaded;
        Unloaded += ViewUnloaded;
    }
    
    private void ViewLoaded(object? sender, RoutedEventArgs e)
    {
        var toplevel = TopLevel.GetTopLevel(this);
    }

    private void ViewUnloaded(object? sender, RoutedEventArgs e)
    {
        // The worker may still be using capture/cascadeClassifier at this point; actually
        // dispose them once it has observed cancellation and fully exited (see
        // Worker_RunWorkerCompleted), not immediately here.
        capture?.Dispose();
        cascadeClassifier?.Dispose();
    }

    private void Start_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TextAddress.Text)) return;
        RTR.Start();
        if (capture.IsOpened())
        {
            return;
        }

        if(int.TryParse(TextAddress.Text, out int index))
        {
            if(!capture.Open(index, VideoCaptureAPIs.ANY))
            {
                _notificationManager?.Show(new Notification("Tip", "Open failed"));
            }
        }
        else if(!capture.Open(TextAddress.Text, VideoCaptureAPIs.ANY))
        {
            _notificationManager?.Show(new Notification("Tip", "Open failed"));
        }

    }

    private void Stop_OnClick(object? sender, RoutedEventArgs e)
    {
        RTR.Stop();
    }
}