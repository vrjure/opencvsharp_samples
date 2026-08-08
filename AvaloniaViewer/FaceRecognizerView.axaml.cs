using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenCvSharp.Face;

namespace AvaloniaViewer;

class FaceItem{
    public string Name { get; set; }
    public string FilePath { get; set; }
    public string Feature { get; set; }
    public IImage Image { get; set; }
}
public partial class FaceRecognizerView : UserControl
{
    private FaceRecognizerSF recognizer;
    public FaceRecognizerView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        recognizer = FaceRecognizerSF.Create("face_recognition_sface_2021dec.onnx", "");
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        recognizer?.Dispose();
    }

    private async void Btn_TranOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await OpenImage();
        TranImageList.ItemsSource = files.Select(f =>
        {
            var feature = GetFaceFeature(f.Path.LocalPath);
            var buffer = ToBinaryFeature(feature);
            feature?.Dispose();
            return new FaceItem()
            {
                Name = Path.GetFileNameWithoutExtension(f.Path.LocalPath),
                Image = new Bitmap(f.Path.LocalPath),
                FilePath = f.Path.LocalPath,
                Feature = Serializer(buffer)
            };
        }).ToList();
    }

    private async void Btn_RecognizeOpen_OnClick(object? sender, RoutedEventArgs e)
    {
        var file = await OpenImage(false);
        if (file.FirstOrDefault() == null) return;
        RecognizeFile.Text = file.FirstOrDefault().Path.LocalPath;
        RecognizeImage.Source = new Bitmap(RecognizeFile.Text);
    }

    private void Btn_Recognize_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(RecognizeFile.Text)) return;
        using var mat = Cv2.ImRead(RecognizeFile.Text, ImreadModes.Grayscale);

        var targetFeature = GetFaceFeature(mat);
        if (targetFeature == null) return;
        var data = TranImageList.ItemsSource as IList<FaceItem>;
        if (data == null) return;

        double maxScore = 0;
        string name = "";
        foreach (var item in data)
        {
            using var matchFeature = ToFeature(Dserializer(item.Feature));
            var result = recognizer.Match(targetFeature, matchFeature);
            Debug.WriteLine($"{item.Name}:{result}");
            if (result > maxScore)
            {
                maxScore = result;
                name = item.Name;
            }
        }
        ResultLabel.Text = $"{name}({maxScore})";
    }

    private async Task<IEnumerable<IStorageFile>> OpenImage(bool multiple = true)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return Enumerable.Empty<IStorageFile>();

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Select Image",
            AllowMultiple = multiple,
            FileTypeFilter = [
            new FilePickerFileType("Image"){ Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"]}]
        });

        return files;
    }

    private Mat? GetFaceFeature(string imagePath)
    {
        using var img = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
        return GetFaceFeature(img);
    }
    
    private Mat? GetFaceFeature(Mat img)
    {
        var size = img.Size();
        using var detector = FaceDetectorYN.Create("face_detection_yunet_2023mar.onnx", "", size);
        using var faces = new Mat();
        var result = detector.Detect(img, faces);
        if (result <= 0 || faces.Rows <=0) return null;
        
        using var alignedFace = new Mat();
        recognizer.AlignCrop(img, faces.Row(0) , alignedFace);

        var feature = new Mat();
        recognizer.Feature(alignedFace, feature);

        return feature;
    }

    private byte[] ToBinaryFeature(Mat feature)
    {
        if (feature == null) return new byte[0];
        byte[] buffer = new byte[512];
        Marshal.Copy(feature.Data, buffer, 0, buffer.Length);
        return buffer;
    }

    private Mat ToFeature(byte[] buffer)
    {
        var feature = new Mat(1,128, MatType.CV_32FC1);
        Marshal.Copy(buffer,0, feature.Data, buffer.Length);
        return feature;
    }

    private string Serializer(byte[] buffer) => Convert.ToBase64String(buffer); 
    private byte[] Dserializer(string base64String) => Convert.FromBase64String(base64String);
}