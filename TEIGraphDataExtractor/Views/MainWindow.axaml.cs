using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using System;
using TEIGraphDataExtractor.ViewModels;
using TEIGraphDataExtractor.Views;

namespace TEIGraphDataExtractor.Views;

public partial class MainWindow : Window
{
    private string _activeCalibrationStep = ""; //hangi nokta secili
    private int _calibrationClicksCount = 0; // ==4 olunca kalibre edilebilecek
    
    public MainWindow()
    {
        InitializeComponent();
    }

    public async void LoadImageButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if(topLevel==null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{
            Title = "Lütfen işlenecek grafiği yükleyiniz.",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files!=null && files.Count >= 1)
        {
            await using var stream = await files[0].OpenReadAsync();
            var bitmap = new Bitmap(stream);

            if (DataContext is MainWindowViewModel vm)
            {
                vm.GraphImage = bitmap;
                vm.SystemStatus = "Grafik başarıyla yüklendi. Lütfen kalibrasyon adımlarına başlayınız.";
            }
        }
    }
    
    public void SelectX1_Click(object? sender, RoutedEventArgs e) { SetCalibrationStep("X1", "📍 Resim üzerinde X1 (Min) noktasını tıklayarak seçin..."); }
    public void SelectX2_Click(object? sender, RoutedEventArgs e) { SetCalibrationStep("X2", "📍 Resim üzerinde X2 (Max) noktasını tıklayarak seçin..."); }
    public void SelectY1_Click(object? sender, RoutedEventArgs e) { SetCalibrationStep("Y1", "📍 Resim üzerinde Y1 (Min) noktasını tıklayarak seçin..."); }
    
   
    public void SelectY2_Click(object? sender, RoutedEventArgs e) { SetCalibrationStep("Y2", "📍 Resim üzerinde Y2 (Max) noktasını tıklayarak seçin..."); }

    private void SetCalibrationStep(string step, string message)
    {
        _activeCalibrationStep = step;
        if (DataContext is MainWindowViewModel vm)
        {
            vm.SystemStatus = message;
        }
    }


    public void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Eğer hiçbir kalibrasyon butonuna basılmadıysa tıklamayı görmezden gel
        if (string.IsNullOrEmpty(_activeCalibrationStep)) return;

        // Farenin tuval üzerindeki X ve Y pikselini al
        var point = e.GetPosition(DrawingCanvas);

        if (DataContext is MainWindowViewModel vm)
        {
            // İlgili pikseli ViewModel'in geçici hafızasına yaz
            switch (_activeCalibrationStep)
            {
                case "X1": vm.MinPixelX = point.X; break;
                case "X2": vm.XMaxPixelX = point.X; break;
                case "Y1": vm.MinPixelY = point.Y; break;
                case "Y2": vm.YMaxPixelY = point.Y; break;
            }

            vm.SystemStatus = $"✅ {_activeCalibrationStep} Noktası Alındı ({point.X:F0}px, {point.Y:F0}px).";
            _activeCalibrationStep = ""; // Seçimi sıfırla
            _calibrationClicksCount++;

            // 4 Nokta da seçildiyse Backend servisine kalibrasyon komutunu gönder!
            if (_calibrationClicksCount >= 4)
            {
                bool success = vm.TryCalibrate();
                _calibrationClicksCount = 0; // Hata olsa da olmasa da bir sonraki deneme için sayacı sıfırla
            }
        }
    }

   
    public void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);

        if (DataContext is MainWindowViewModel vm && vm.Converter.IsCalibrated)
        {
            // KALİBRASYON YAPILDIYSA: Pikseli formüle sok ve gerçek mühendislik değerini sağ alta yaz
            var realCoords = vm.Converter.PixelToRealWorld(point.X, point.Y);
            CoordinateText.Text = $"Gerçek X: {realCoords.RealX} | Gerçek Y: {realCoords.RealY}";
        }
        else
        {
            // KALİBRASYON YOKSA: Sadece ham piksel değerlerini göster
            CoordinateText.Text = $"Piksel X: {point.X:F0}px | Piksel Y: {point.Y:F0}px";
        }
    }
}