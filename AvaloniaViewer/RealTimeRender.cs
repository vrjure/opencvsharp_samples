using System.IO;
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

    private readonly Mat _opencv_logo;
    private Mat _logo_mask = new Mat();
    private Mat _logo_mask_inv = new Mat();
    public VideoCaptureVisualHandler(OpenCvSharp.VideoCapture vc, CascadeClassifier cascadeClassifier)
    {
        _vc = vc;
        _cascadeClassifier = cascadeClassifier;
        _opencv_logo = new Mat();
        using var logo = Cv2.ImRead("OpenCV_logo_white.png");

        using var logo_gray = new Mat();
        Cv2.CvtColor(logo, logo_gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Threshold(logo_gray, _logo_mask, 10, 255, ThresholdTypes.Binary);
        Cv2.BitwiseNot(_logo_mask, _logo_mask_inv);

        Cv2.BitwiseAnd(logo,logo,_opencv_logo, _logo_mask);

        if (!Directory.Exists("output"))
        {
            Directory.CreateDirectory("output");
        }

        Cv2.ImWrite("output/opencv_logo.png", _opencv_logo);
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
                    
                    //var roi = new Mat(frame, new Rect((int)x, (int)y, (int)w, (int)h));
                    Cv2.Rectangle(frame, new Rect((int)x,(int)y,(int)w, (int)h), Scalar.YellowGreen);
                    //frame[new Rect(0,0, (int)roi.Width,(int)roi.Height)] = roi;
                }
            }
        }
        else
        {
            var rects = _cascadeClassifier.DetectMultiScale(grayImg, 1.1, 5, HaarDetectionTypes.ScaleImage, new OpenCvSharp.Size(30, 30));
            foreach (var rect in rects)
            {
                Cv2.Rectangle(frame, rect, Scalar.YellowGreen);
            }
        }

        var logoROI = frame[new Rect(0,0,_opencv_logo.Width, _opencv_logo.Height)];
        
        using var bg = new Mat();
        Cv2.BitwiseAnd(logoROI,logoROI,bg, _logo_mask_inv);

        using var added = new Mat();
        Cv2.Add(bg, _opencv_logo, added);

        frame[new Rect(0,0,_opencv_logo.Width, _opencv_logo.Height)] = added;
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