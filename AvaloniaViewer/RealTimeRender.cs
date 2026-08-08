using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace AvaloniaViewer;

public class RealTimeRender : Control
{
    public CompositionCustomVisualHandler VisualHandler { get; set; }
    private CompositionCustomVisual _customVisual;
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        var visual = ElementComposition.GetElementVisual(this);
        if (visual == null) return;
        var compositor = visual.Compositor;
        if (_customVisual == null)
        {
            _customVisual = compositor.CreateCustomVisual(VisualHandler);
            ElementComposition.SetElementChildVisual(this,_customVisual);
        }

    }
    

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_customVisual == null) return;
        _customVisual.Size = new Vector(e.NewSize.Width, e.NewSize.Height);
    }

    public void Start()
    {
        _customVisual.SendHandlerMessage(VideoCaptureVisualHandler.Start);
    }

    public void Stop()
    {
        _customVisual?.SendHandlerMessage(VideoCaptureVisualHandler.Stop);
    }
}

class VideoCaptureVisualHandler : CompositionCustomVisualHandler
{
    public static object Start = new object();
    public static object Stop = new object();
    private OpenCvSharp.VideoCapture _vc;
    private CascadeClassifier _cascadeClassifier;
    private FaceDetectorYN _faceDetector;
    private bool _running;
    public VideoCaptureVisualHandler(OpenCvSharp.VideoCapture vc, CascadeClassifier cascadeClassifier)
    {
        _vc = vc;
        _cascadeClassifier = cascadeClassifier;
    }
    public override void OnRender(ImmediateDrawingContext drawingContext)
    {
        if (_vc == null || _vc.IsDisposed || !_vc.IsOpened()) return;
        if (_faceDetector == null)
        {
            _faceDetector = FaceDetectorYN.Create("face_detection_yunet_2023mar.onnx", "",
                new Size(_vc.FrameWidth, _vc.FrameHeight), 0.7f, targetId:Target.CPU);
        }

        using var frame = _vc.RetrieveMat();
        if (frame.Empty()) return;
        using var grayImg = new Mat();
        Cv2.CvtColor(frame, grayImg, ColorConversionCodes.BGR2GRAY);
        if (_faceDetector != null)
        {
            var faces = new Mat();
            var result = _faceDetector.Detect(grayImg, faces);
            if (result > 0)
            {
                var rowAccessor = faces.AsRows<float>();
                var rows = faces.Rows;
                for (int i = 0; i < rows; i++)
                {
                    var row = rowAccessor[i];
                    var x = row[0];
                    var y = row[1];
                    var w = row[2];
                    var h = row[3];
                    Cv2.Rectangle(frame, new Rect((int)x,(int)y,(int)w, (int)h), Scalar.YellowGreen);
                }
            }
        }
        else
        {
            var rects = _cascadeClassifier.DetectMultiScale(grayImg, 1.1, 5, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(30, 30));
            foreach (var rect in rects)
            {
                Cv2.Rectangle(frame, rect, Scalar.Red);
            }
        }

        
        drawingContext.DrawBitmap(frame.ToAvaloniaBitmap(), GetRenderBounds());
    }

    public override void OnMessage(object message)
    {
        base.OnMessage(message);
        if (message == Start)
        {
            _running = true;
            RegisterForNextAnimationFrameUpdate();
        }
        else if (message == Stop)
        {
            _running = false;
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        base.OnAnimationFrameUpdate();
        if (_running)
        {
            Invalidate();
            RegisterForNextAnimationFrameUpdate();
        }

    }
}