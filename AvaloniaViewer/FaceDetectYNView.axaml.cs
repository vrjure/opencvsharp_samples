using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using OpenCvSharp;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace AvaloniaViewer;

public partial class FaceDetectYNView : UserControl
{
    public FaceDetectYNView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
       
    }

    private void Btn_Detect_OnClick(object? sender, RoutedEventArgs e)
    {
        using var soureImg = Cv2.ImRead("largest_selfie.jpg");
        using var gray = new Mat();
        Cv2.CvtColor(soureImg, gray, ColorConversionCodes.BGR2GRAY);
        using var faces = new Mat();
        using var _faceDetector = FaceDetectorYN.Create("face_detection_yunet_2023mar.onnx", "", new Size(soureImg.Width, soureImg.Height), 0.7f);
        
        var result = _faceDetector.Detect(gray, faces);
        if (result > 0)
        {
            var rowAccessor = faces.AsRows<float>();
            var rows = faces.Rows;
            for (int i = 0; i < faces.Rows; i++)
            {
                var row = rowAccessor[i];
                var x = row[0];
                var y = row[1];
                var w = row[2];
                var h = row[3];
                Cv2.Rectangle(soureImg, new Rect((int)x,(int)y,(int)w,(int)h), Scalar.GreenYellow);
            }
        }
        FaceImage.Source = soureImg.ToAvaloniaBitmap();
    }
}