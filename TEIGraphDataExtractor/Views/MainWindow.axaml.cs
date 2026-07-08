using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media; //brushes
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
    private bool _isDrawModeActive=false;
    private System.Collections.Generic.Dictionary<string, Ellipse> _calibrationMarkers = new();
    private Avalonia.Point _lastCollectedPoint = new Avalonia.Point(0, 0);

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
                vm.SystemStatus = "Grafik yüklendi. Lütfen kalibrasyon adımlarına başlayınız.";
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

            if (_calibrationMarkers.ContainsKey(_activeCalibrationStep))
            {
                DrawingCanvas.Children.Remove(_calibrationMarkers[_activeCalibrationStep]);
            }
            IBrush markerColor = _activeCalibrationStep switch
            {
                "X1" => Brushes.Cyan,      // Parlak Kırmızı
                "X2" => Brushes.DarkOrange,   // Turuncu
                "Y1" => Brushes.DodgerBlue,   // Canlı Mavi
                "Y2" => Brushes.LimeGreen,    // Fıstık Yeşili
                _    => Brushes.White
            };

            var marker = new Ellipse
            {
                Width = 10,
                Height=10,
                Fill = markerColor,
                Stroke=Brushes.White,
                StrokeThickness=2
            };
            
            Canvas.SetLeft(marker, point.X-5);
            Canvas.SetTop(marker, point.Y-5);
            DrawingCanvas.Children.Add(marker);
            _calibrationMarkers[_activeCalibrationStep] = marker;

            vm.SystemStatus = $"✅ {_activeCalibrationStep} Noktası Güncellendi ({point.X:F0}px, {point.Y:F0}px).";
            _activeCalibrationStep = ""; // Seçimi sıfırla
            _calibrationClicksCount++;

            // 4 Nokta da seçildiyse Backend servisine kalibrasyon komutunu gönder!
            if (_calibrationClicksCount >= 4)
            {
                bool success = vm.TryCalibrate();
                vm.SystemStatus = "4 kalibrasyon noktası başarı ile seçildi. Uygun değerler ile 'Kalibrasyonu Tamamla' butonuna basabilirsiniz.";
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
        
            var properties = e.GetCurrentPoint(DrawingCanvas).Properties;
            if(_isDrawModeActive && properties.IsLeftButtonPressed)
            {
                double distance = Math.Sqrt(Math.Pow(point.X - _lastCollectedPoint.X, 2) + Math.Pow(point.Y - _lastCollectedPoint.Y, 2));
                //oklid mesafesi
                if (distance >= 5)
                {
                    var dataDot = new Ellipse{ Width = 4, Height = 4, Fill = Brushes.Red };
                    Canvas.SetLeft(dataDot, point.X - 2);
                    Canvas.SetTop(dataDot, point.Y - 2);
                    DrawingCanvas.Children.Add(dataDot);

                    // geçici noktayı güncelle
                    _lastCollectedPoint = point;
                    Console.WriteLine($"Toplanan Nokta: X={realCoords.RealX}, Y={realCoords.RealY}");
                
                }
            }
        }




        else
        {
            // KALİBRASYON YOKSA: Sadece ham piksel değerlerini göster
            CoordinateText.Text = $"Piksel X: {point.X:F0}px | Piksel Y: {point.Y:F0}px";
        }
    }

    public void DrawModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm){
            if(!vm.Converter.IsCalibrated)
            {
            vm.SystemStatus = "Önce kalibrasyonu tamamlamalısınız!";
            return;
            }
        
        _isDrawModeActive = !_isDrawModeActive;

        vm.SystemStatus = _isDrawModeActive
            ? "✒️ Kalem Modu AKTİF. Farenin sol tuşuna basılı tutarak çizim yapın." 
            : "✒️ Kalem Modu KAPATILDI.";
        }
    }

    public void CalibrateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            bool success = vm.TryCalibrate();
        }
    }

    public void ResetButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RealXMin = 0.0;
            vm.RealXMax = 0.0;
            vm.RealYMin = 0.0;
            vm.RealYMax = 0.0;
            vm.MinPixelX = 0.0;
            vm.MinPixelY = 0.0;
            vm.XMaxPixelX = 0.0;
            vm.YMaxPixelY = 0.0;

            // 2. Ekranda çizilmiş olan tüm kırmızı kalibrasyon noktalarını ve mavi kalem çizgilerini temizle
            DrawingCanvas.Children.Clear();
            
            // 3. Kod tarafındaki hafıza sayaçlarını temizle
            _calibrationMarkers.Clear();
            _calibrationClicksCount = 0;
            _activeCalibrationStep = "";
            
            vm.SystemStatus = "🔄 Tüm kalibrasyon ve grafik verileri başarıyla sıfırlandı.";
        }
    }

}