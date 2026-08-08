using System.Linq;
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
    private string _file;

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _resultImage.Dispose();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if(btn == null) return;
        var content = btn.Content.ToString();
        switch (content)
        {
            case "Open":
                Open();
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
        }
    }

    private async void Open()
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

        _file = file[0].Path.LocalPath;
        ResultImage.Source = new Bitmap(_file);
        _resultImage = Cv2.ImRead(_file);
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
        Cv2.GaussianBlur(_resultImage, blurred, new Size(5,5), 0);
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

    private void UpdateImage(Mat img)
    {
        _resultImage?.Dispose();
        _resultImage = img;
        ResultImage.Source = _resultImage.ToAvaloniaBitmap();
    }
}