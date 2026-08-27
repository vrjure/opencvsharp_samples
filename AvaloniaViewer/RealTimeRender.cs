using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
        if (visual == null || VisualHandler == null) return;
        var compositor = visual.Compositor;
        if (_customVisual == null)
        {
            _customVisual = compositor.CreateCustomVisual(VisualHandler);
            ElementComposition.SetElementChildVisual(this,_customVisual);
        }

    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        
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

    public void OpticalFlowStart()
    {
        _customVisual?.SendHandlerMessage(VideoCaptureVisualHandler.OpticalFlowStart);
    }

    public void OpticalFlowStop()
    {
        _customVisual?.SendHandlerMessage(VideoCaptureVisualHandler.OpticalFlowStop);
    }

    public void AddLogo()
    {
        _customVisual?.SendHandlerMessage(VideoCaptureVisualHandler.AddLogo);
    }

    public void RemoveLogo()
    {
        _customVisual?.SendHandlerMessage(VideoCaptureVisualHandler.RemoveLogo);
    }

    public void FaceDetectEnable()
    {
        _customVisual?.SendHandlerMessage(VideoCaptureVisualHandler.FaceDetectEnable);
    }

    public void FaceDetectDisable()
    {
        _customVisual?.SendHandlerMessage(VideoCaptureVisualHandler.FaceDetectDisable);
    }
}

class VideoCaptureVisualHandler : CompositionCustomVisualHandler, IDisposable
{
    public static object Start = new object();
    public static object Stop = new object();
    public static object OpticalFlowStart = new object();
    public static object OpticalFlowStop = new object();
    public static object AddLogo = new object();
    public static object RemoveLogo = new object();
    public static object FaceDetectEnable = new object();
    public static object FaceDetectDisable = new object();

    private OpenCvSharp.VideoCapture _vc;
    private CascadeClassifier _cascadeClassifier;
    private FaceDetectorYN _faceDetector;

    private readonly Mat _opencv_logo;
    private Mat _logo_mask = new Mat();
    private Mat _logo_mask_inv = new Mat();
    private Mat _oldGrayFrame;
    private Scalar[] _colors;
    private Mat _opticalFlowMask;

    private object _startStop_op;
    private object _opticalFlow_OP;
    private object _logo_op;
    private object _faceDetect_op;
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

        using var frame = _vc.RetrieveMat();
        
        if (frame.Empty()) return;
        using var grayImg = new Mat();
        Cv2.CvtColor(frame, grayImg, ColorConversionCodes.BGR2GRAY);

        if (_faceDetect_op == FaceDetectEnable)
        {
            if (_faceDetector == null)
            {
                _faceDetector = FaceDetectorYN.Create("face_detection_yunet_2023mar.onnx", "",
                    new Size(_vc.FrameWidth, _vc.FrameHeight), 0.7f, targetId:Target.CPU);
            }

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
        }
        
        if (_logo_op == AddLogo)
        {
            using var logoROI = frame[new Rect(0,0,_opencv_logo.Width, _opencv_logo.Height)];
        
            using var bg = new Mat();
            Cv2.BitwiseAnd(logoROI,logoROI,bg, _logo_mask_inv);

            using var added = new Mat();
            Cv2.Add(bg, _opencv_logo, added);

            frame[new Rect(0,0,_opencv_logo.Width, _opencv_logo.Height)] = added;
        }

        if (_opticalFlow_OP == OpticalFlowStart)
        {
            if (_oldGrayFrame == null)
            {
                _oldGrayFrame = grayImg.Clone();
                return;
            }

            if (_opticalFlowMask == null)
            {
                _opticalFlowMask = Mat.Zeros(frame.Size(), frame.Type()).ToMat();
            }

            var prevPts = Cv2.GoodFeaturesToTrack(grayImg, 100, 0.3, 7, default, 7, true, 0.04);
            Point2f[] nextPts = new Point2f[prevPts.Length];
            Cv2.CalcOpticalFlowPyrLK(_oldGrayFrame, grayImg, prevPts, ref nextPts, out var status, out var err);

            for (int i = 0; i < status.Length; i++)
            {
                if (status[i] == 1)
                {
                    if (_colors == null)
                    {
                        var rand = new Random();
                        _colors = Enumerable.Range(0, 100)
                                   .Select(_ => Scalar.FromRgb(rand.Next(0, 255), rand.Next(0, 255), rand.Next(0, 255)))
                                   .ToArray();
                    }
                    Cv2.Line(_opticalFlowMask, (OpenCvSharp.Point)prevPts[i], (OpenCvSharp.Point)nextPts[i], _colors[i%100], 2);
                    Cv2.Circle(frame, (OpenCvSharp.Point)prevPts[i],5, _colors[i%100],-1);

                }
            }
            Cv2.Add(frame, _opticalFlowMask, frame);

            _oldGrayFrame.Dispose();
            _oldGrayFrame = null;
            _oldGrayFrame = grayImg.Clone();
        }

        var renderRect = GetRenderBounds();
        var scale = renderRect.Width / frame.Width;
        var height = frame.Height * scale;

        drawingContext.DrawBitmap(frame.ToAvaloniaBitmap(), new Avalonia.Rect(0, (renderRect.Height - height)/2, renderRect.Width, height));
    }

    public override void OnMessage(object message)
    {
        base.OnMessage(message);
        if (message == Start || message == Stop)
        {
            _startStop_op = message;
            if(_startStop_op == Start)
            {
                RegisterForNextAnimationFrameUpdate();
            }
        }
        else if (message == OpticalFlowStart || message == OpticalFlowStop)
        {
            _opticalFlow_OP = message;
        }
        else if (message == FaceDetectEnable || message == FaceDetectDisable)
        {
            _faceDetect_op = message;
        }
        else if (message == AddLogo || message == RemoveLogo)
        {
            _logo_op = message;
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        base.OnAnimationFrameUpdate();
        if (_startStop_op == Start)
        {
            Invalidate();
            RegisterForNextAnimationFrameUpdate();
        }

    }

    public void Dispose()
    {
        _logo_mask?.Dispose();
        _logo_mask_inv?.Dispose();
        _oldGrayFrame?.Dispose();
        _opencv_logo?.Dispose();
        _opticalFlowMask?.Dispose();
    }
}