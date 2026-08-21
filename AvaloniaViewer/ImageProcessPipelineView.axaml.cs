using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace AvaloniaViewer;

public partial class ImageProcessPipelineView : UserControl
{
    public ImageProcessPipelineView()
    {
        InitializeComponent();
    }
    

    private Mat _resultImage;
    private Mat? _maskImage;
    private string _file;

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _resultImage.Dispose();
        _maskImage?.Dispose();
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if(btn == null) return;
        var content = btn.Content.ToString();
        switch (content)
        {
            case "Open":
                _file = await OpenImage();
                ResultImage.Source = new Bitmap(_file);
                _resultImage = Cv2.ImRead(_file);
                break;
            case "AddMask":
                var maskFile = await OpenImage();
                _maskImage = Cv2.ImRead(maskFile);
                UpdateImageMask();
                break;
            case "GrayScale":
                GrayScale();
                break;
            case "GaussianBlur":
                GaussianBlur();
                break;
            case "Threshold":
                Threshold();
                break;
            case "Morphology":
                Morphology();
                break;
            case "Contours":
                Contours();
                break;
            case "InRange":
                InRange();
                break;
            case "Move+":
                Move(10,10);
                break;
            case "Move-":
                Move(-10,-10);
                break;
            case "Rotation":
                Rotation();
                break;
            case "Affine":
                Affine();
                break;
            case "Perspective":
                Perspective();
                break;
            case "AdaptiveThreshold":
                AdaptiveThreshold();
                break;
            case "Filter2D":
                Filter2D();
                break;
            case "BilateralFilter":
                BilateralFilter();
                break;
            case "Gradient":
                Gradient();
                break;
        }
    }
    
    private async Task<string> OpenImage()
    {
        var toplevel = TopLevel.GetTopLevel(this);
        var file = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "select image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("image") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"] }
            ]
        });
        if (file.Count == 0)
        {
            ResultImage.Source = null;
        }

        return file[0].Path.LocalPath;
    }

    private void GrayScale()
    {
        var gray = new Mat();
        Cv2.CvtColor(_resultImage, gray, ColorConversionCodes.BGR2GRAY);
        UpdateImage(gray);
    }

    private void GaussianBlur()
    {
        var blurred = new Mat();
        Cv2.GaussianBlur(_resultImage, blurred, new Size(15,15), 0);
        UpdateImage(blurred);
    }

    private void Threshold()
    {
        var binary = new Mat();
        Cv2.Threshold(_resultImage, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        UpdateImage(binary);
    }

    private void Morphology()
    {
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        var cleaned = new Mat();
        Cv2.MorphologyEx(_resultImage, cleaned, MorphTypes.Open, kernel);
        UpdateImage(cleaned);
    }

    private void Contours()
    {
        Cv2.FindContours(
            _resultImage,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        Point[][] significantContours = contours.Where(contour => Cv2.ContourArea(contour) >= 100)
            .ToArray();

        var annotated = Cv2.ImRead(_file);
        Cv2.DrawContours(
            annotated,
            significantContours,
            contourIdx: -1,
            color: Scalar.Red,
            thickness: 2);
        
        UpdateImage(annotated);
    }

    private void InRange()
    {
        if (_resultImage == null) return;
        using var hsv = new Mat();
        Cv2.CvtColor(_resultImage, hsv, ColorConversionCodes.BGR2HSV);
        using var range = new Mat();
        var lowBlue = new Scalar(90,50,50);
        var upperBlue = new Scalar(130,255,255);

        using var mask = new Mat();
        Cv2.InRange(hsv, lowBlue, upperBlue, mask);

        var result = new Mat();
        Cv2.BitwiseAnd(_resultImage, _resultImage, result, mask);

        UpdateImage(result);
    }

    private void Move(float x, float y)
    {
        if (_resultImage == null) return;
        using var mat = Mat.FromPixelData(2,3, MatType.CV_32FC1,new float[]{1,0,x,0,1,y});
        var moved = new Mat();
        Cv2.WarpAffine(_resultImage, moved, mat, new OpenCvSharp.Size(_resultImage.Cols, _resultImage.Rows));
        UpdateImage(moved);
    }

    private void Rotation()
    {
        if (_resultImage == null) return;

        using var rotation = Cv2.GetRotationMatrix2D(new Point2f(_resultImage.Width/2, _resultImage.Height/2), 90, 1);
        var rotated = new Mat();
        Cv2.WarpAffine(_resultImage, rotated, rotation, new Size(_resultImage.Width, _resultImage.Height));
        UpdateImage(rotated);
    }

    private void Affine()
    {
        if (_resultImage == null) return;

        using var transform = Cv2.GetAffineTransform(
        [
            new(50,50),
            new(200,50),
            new(50,200)
        ],
        [
            new(10,100),
            new(200,50),
            new(100,250)
        ]);

        var result = new Mat();
        Cv2.WarpAffine(_resultImage, result, transform, new Size(_resultImage.Width, _resultImage.Height));
        UpdateImage(result);
    }

    private void Perspective()
    {
        if (_resultImage == null) return;

        var w = _resultImage.Width;
        var h = _resultImage.Height;
        Cv2.Line(_resultImage, new(w/2,0), new(w/2,h), Scalar.LightGreen, 2);
        Cv2.Line(_resultImage, new(0,h/2), new(w,h/2), Scalar.LightGreen, 2);

        using var transform = Cv2.GetPerspectiveTransform([
            new(50,50),
            new(w-100,50),
            new(100,h-50),
            new(w-150,h-100)
        ],
        [
            new(0,0),
            new(w,0),
            new(0,h),
            new(w,h)
        ]);

        var result = new Mat();
        Cv2.WarpPerspective(_resultImage, result, transform, new(w,h));
        UpdateImage(result);
    }

    private void AdaptiveThreshold()
    {
        if (_resultImage == null) return;
        var w = _resultImage.Width;
        var h = _resultImage.Height;

        using var gray = new Mat();
        Cv2.CvtColor(_resultImage, gray, ColorConversionCodes.BGR2GRAY);

        using var medianBlur = new Mat();
        Cv2.MedianBlur(gray, medianBlur, 5);

        using var roiLeft = new Mat(medianBlur, new OpenCvSharp.Rect(0,0,w/2,h));
        using var roiRight = new Mat(medianBlur, new OpenCvSharp.Rect(w/2,0,w/2,h));

        using var meanC = new Mat();
        Cv2.AdaptiveThreshold(roiLeft, meanC, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 11, 2);

        using var gaussianC = new Mat();
        Cv2.AdaptiveThreshold(roiRight, gaussianC, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11,2);

        gray[new OpenCvSharp.Rect(0,0,w/2,h)] = meanC;
        gray[new OpenCvSharp.Rect(w/2,0,w/2,h)] = gaussianC;

        ResultImage.Source = gray.ToAvaloniaBitmap();
    }

    private void Filter2D()
    {
        if (_resultImage == null) return;

        var kernel = Mat.Ones(5,5, MatType.CV_32FC1) / 25f;
        using var result = new Mat();
        Cv2.Filter2D(_resultImage, result, _resultImage.Type().Depth, kernel);

        ResultImage.Source = result.ToAvaloniaBitmap();
    }

    private void BilateralFilter()
    {
        if (_resultImage == null) return;

        using var result = new Mat();
        Cv2.BilateralFilter(_resultImage, result, 15, 300,100);
        ResultImage.Source = result.ToAvaloniaBitmap();
    }

    private void Gradient()
    {
        if (_resultImage == null) return;

        //using var gray = new Mat();
        //Cv2.CvtColor(_resultImage, gray, ColorConversionCodes.BGR2GRAY);

        using var laplacian = new Mat();
        Cv2.Laplacian(_resultImage, laplacian, new MatType(-1), 15);

        ResultImage.Source = laplacian.ToAvaloniaBitmap();
    }

    private void UpdateImage(Mat img)
    {
        _resultImage?.Dispose();
        _resultImage = img;
        ResultImage.Source = _resultImage.ToAvaloniaBitmap();
    }

    private void UpdateImageMask()
    {
        if (_maskImage == null) return;
        using var gray = new Mat();
        Cv2.CvtColor(_maskImage, gray, ColorConversionCodes.BGR2GRAY);
        
        // using var gaussianBlur = new Mat();
        // Cv2.GaussianBlur(gray, gaussianBlur, new(1,1), 0);

        // using var edges = new Mat();
        // Cv2.Canny(gray, edges, 100,200);

        using var mask = new Mat();
        Cv2.Threshold(gray, mask, 0, 255, ThresholdTypes.Binary);

        using var mask_inv = new Mat();
        Cv2.BitwiseNot(mask, mask_inv);

        using var bg = new Mat();
        using var roi = _resultImage[new OpenCvSharp.Rect(0,0, _maskImage.Width, _maskImage.Height)];
        Cv2.BitwiseAnd(roi, roi, bg, mask_inv);

        using var fg = new Mat();
        Cv2.BitwiseAnd(_maskImage, _maskImage, fg, mask);

        using var added = new Mat();
        Cv2.Add(bg, fg, added);

        _resultImage[new OpenCvSharp.Rect(0,0,_maskImage.Width, _maskImage.Height)] = added;

        ResultImage.Source = _resultImage.ToAvaloniaBitmap();
    }
}