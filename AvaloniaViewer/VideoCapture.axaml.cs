using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Rendering.Composition;
using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace AvaloniaViewer;

public partial class VideoCapture : UserControl
{
    private readonly OpenCvSharp.VideoCapture capture;
    private readonly CascadeClassifier cascadeClassifier;
    private VideoCaptureVisualHandler _videoVisualHandler;
    public VideoCapture()
    {
        InitializeComponent();
        capture = new OpenCvSharp.VideoCapture();
        cascadeClassifier = new CascadeClassifier("haarcascade_frontalface_default.xml");
        _videoVisualHandler = new VideoCaptureVisualHandler(capture, cascadeClassifier);
        RTR.VisualHandler = _videoVisualHandler;
        Loaded += ViewLoaded;
        Unloaded += ViewUnloaded;
    }
    
    private void ViewLoaded(object? sender, RoutedEventArgs e)
    {
        
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
        capture.Open(TextAddress.Text, VideoCaptureAPIs.ANY);
    }

    private void Stop_OnClick(object? sender, RoutedEventArgs e)
    {
        RTR.Stop();
    }
}