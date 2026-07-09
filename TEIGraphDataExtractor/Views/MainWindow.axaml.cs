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
    private bool _isDrawModeActive = false;
    private System.Collections.Generic.Dictionary<string, Ellipse> _calibrationMarkers = new();
    private Avalonia.Point _lastCollectedPoint = new Avalonia.Point(0, 0);
    
    // [HEM GERİ AL HEM SEÇEREK SİLME İÇİN DEĞİŞTİ]: Stack yerine List kullanıyoruz!
    private System.Collections.Generic.List<Ellipse> _drawnDataDots = new(); 
    
    private bool _isSingleAddModeActive = false; // add point ? 
    private bool _isDeleteModeActive = false;    // [YENİ]: Tıklayarak seçip silme modu

    public MainWindow()
    {
        InitializeComponent();
    }

    public async void LoadImageButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if(topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{
            Title = "Lütfen işlenecek grafiği yükleyiniz.",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files != null && files.Count >= 1)
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
        if (DataContext is MainWindowViewModel vm) vm.SystemStatus = message;
    }

    public void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Farenin tuval üzerindeki X ve Y pikselini al
        var point = e.GetPosition(DrawingCanvas);

        // ====================================================================
        // [1. ÖZELLİK]: TIKLAYARAK SEÇİP SİLME (AVCI MODU)
        // Eğer Silme Modu aktifse, tuvalde tıkladığımız yere en yakın noktayı bul ve yok et!
        // ====================================================================
        if (_isDeleteModeActive && DataContext is MainWindowViewModel vmDelete)
        {
            Ellipse? closestDot = null;
            double minDist = 15.0; // 15 piksel yarıçap toleransı (Hit-Box)

            // Ekrandaki tüm kırmızı noktalardan farenin tıkladığı yere en yakın olanı buluyoruz
            foreach (var dot in _drawnDataDots)
            {
                double dotX = Canvas.GetLeft(dot) + (dot.Width / 2);
                double dotY = Canvas.GetTop(dot) + (dot.Height / 2);
                double dist = Math.Sqrt(Math.Pow(point.X - dotX, 2) + Math.Pow(point.Y - dotY, 2));
                
                if (dist <= minDist)
                {
                    minDist = dist;
                    closestDot = dot;
                }
            }

            // 15 piksel yakınımızda bir nokta varsa hem ekrandan hem listeden sil!
            if (closestDot != null)
            {
                DrawingCanvas.Children.Remove(closestDot);
                _drawnDataDots.Remove(closestDot); // List olduğu için ortadan eleman silebiliyoruz!
                
                // Backend motorunu tetikleyip veritabanı/RAM listesinden de sildiriyoruz
                vmDelete.TryDeletePointAtPixel(point.X, point.Y, 15.0);
            }
            else
            {
                vmDelete.SystemStatus = "ℹ️ Tıkladığınız yerde silinecek bir nokta bulunamadı.";
            }
            return; // Silme yapıldığı için aşağıdaki çizim kodlarına geçmeden çık!
        }
        // ====================================================================

        if (DataContext is MainWindowViewModel vm)
        {
            if (!string.IsNullOrEmpty(_activeCalibrationStep))
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
                    "X1" => Brushes.Cyan,         // Parlak Kırmızı
                    "X2" => Brushes.DarkOrange,   // Turuncu
                    "Y1" => Brushes.DodgerBlue,   // Canlı Mavi
                    "Y2" => Brushes.LimeGreen,    // Fıstık Yeşili
                    _    => Brushes.White
                };

                var marker = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = markerColor,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                
                Canvas.SetLeft(marker, point.X - 5);
                Canvas.SetTop(marker, point.Y - 5);
                DrawingCanvas.Children.Add(marker);
                _calibrationMarkers[_activeCalibrationStep] = marker;

                vm.SystemStatus = $"✅ {_activeCalibrationStep} Noktası Güncellendi ({point.X:F0}px, {point.Y:F0}px).";
                _activeCalibrationStep = ""; // Seçimi sıfırla
                _calibrationClicksCount++;

                // 4 Nokta da seçildiyse Backend servisine kalibrasyon komutunu gönder!
                if (_calibrationClicksCount >= 4)
                {
                    vm.SystemStatus = "4 kalibrasyon noktası başarı ile seçildi. 'Kalibrasyonu Tamamla' butonuna basabilirsiniz.";
                    _calibrationClicksCount = 0;
                }
                return;
            }

            if (_isSingleAddModeActive && string.IsNullOrEmpty(_activeCalibrationStep))
            {
                var newPoint = vm.CaptureStreamPoint(point.X, point.Y);
                if (newPoint != null)
                {
                    var dataDot = new Ellipse { Width = 4, Height = 4, Fill = Brushes.Red };
                    Canvas.SetLeft(dataDot, point.X - 2);
                    Canvas.SetTop(dataDot, point.Y - 2);
                    DrawingCanvas.Children.Add(dataDot); // Ekrana ekle
                    _drawnDataDots.Add(dataDot);         // [DEĞİŞTİ]: Push yerine Add

                    vm.SystemStatus = $"📍 Nokta eklendi: X={newPoint.XValue:F3}, Y={newPoint.YValue:F3}";
                }
            }
        }
    }

    public void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);

        if (DataContext is MainWindowViewModel vm && vm.Converter.IsCalibrated)
        {
            var realCoords = vm.Converter.PixelToRealWorld(point.X, point.Y);
            CoordinateText.Text = $"Gerçek X: {realCoords.RealX} | Gerçek Y: {realCoords.RealY}";
        
            var properties = e.GetCurrentPoint(DrawingCanvas).Properties;
            
            // Silme modundayken yanlışlıkla çizim yapmamak için !_isDeleteModeActive kontrolü eklendi
            if (_isDrawModeActive && !_isDeleteModeActive && properties.IsLeftButtonPressed)
            {
                double distance = Math.Sqrt(Math.Pow(point.X - _lastCollectedPoint.X, 2) + Math.Pow(point.Y - _lastCollectedPoint.Y, 2));
                if (distance >= 5)
                {
                    var newPoint = vm.CaptureStreamPoint(point.X, point.Y);
                    if (newPoint != null)
                    {
                        var dataDot = new Ellipse { Width = 4, Height = 4, Fill = Brushes.Red };
                        Canvas.SetLeft(dataDot, point.X - 2);
                        Canvas.SetTop(dataDot, point.Y - 2);

                        DrawingCanvas.Children.Add(dataDot);
                        _drawnDataDots.Add(dataDot); // [DEĞİŞTİ]: Push yerine Add
                        _lastCollectedPoint = point;
                    }
                }
            }
        }
        else
        {
            CoordinateText.Text = $"Piksel X: {point.X:F0}px | Piksel Y: {point.Y:F0}px";
        }

    // ==========================================
    // 🔍 BÜYÜTEÇ MOTORU (CROP TABANLI - SAĞLAM YÖNTEM)
    // ==========================================
    // [YENİ YAKLAŞIM]: Transform/Scale/Translate matematiği Viewbox ve zoom
    // butonlarıyla birlikte güvenilmez sonuç verdiği için tamamen terk edildi.
    // Bunun yerine kaynak resimden farenin GERÇEK piksel konumunu merkez alan
    // küçük bir dikdörtgen KESİYORUZ (CroppedBitmap) ve doğrudan büyüteç
    // kutusuna basıyoruz. Böylece herhangi bir Transform/Viewbox/Scroll
    // hesaplama hatası büyüteç sonucunu etkileyemez.
    var magnifierImage = this.FindControl<Image>("MagnifierImage");
    var magnifierGrid = this.FindControl<Grid>("MagnifierGrid");

    if (DataContext is MainWindowViewModel vmMag &&
        magnifierImage != null && magnifierGrid != null &&
        DrawingCanvas.Bounds.Width > 0 && DrawingCanvas.Bounds.Height > 0 &&
        vmMag.GraphImage != null && magnifierGrid.Bounds.Width > 0 && magnifierGrid.Bounds.Height > 0)
    {
        double imgPixelW = vmMag.GraphImage.PixelSize.Width;
        double imgPixelH = vmMag.GraphImage.PixelSize.Height;

        double boxW = DrawingCanvas.Bounds.Width;   // Ana görüntü kutusunun genişliği (örn. 800)
        double boxH = DrawingCanvas.Bounds.Height;  // Ana görüntü kutusunun yüksekliği (örn. 600)

        // 1. Stretch="Uniform" resmi kutunun İÇİNDE hangi boyutta ve hangi
        //    ofsetle (letterbox boşluğuyla) çizdiğini hesapla
        double uniformScale = Math.Min(boxW / imgPixelW, boxH / imgPixelH);
        double renderedW = imgPixelW * uniformScale;
        double renderedH = imgPixelH * uniformScale;
        double offsetX = (boxW - renderedW) / 2.0;
        double offsetY = (boxH - renderedH) / 2.0;

        // 2. Farenin GERÇEK RESMİN İÇİNDEKİ yüzdesi (0.0 - 1.0)
        double percentX = (point.X - offsetX) / renderedW;
        double percentY = (point.Y - offsetY) / renderedH;
        percentX = Math.Clamp(percentX, 0.0, 1.0);
        percentY = Math.Clamp(percentY, 0.0, 1.0);

        // 3. Bu yüzdeyi KAYNAK BİTMAP'İN GERÇEK PİKSEL koordinatına çevir
        double imagePixelX = percentX * imgPixelW;
        double imagePixelY = percentY * imgPixelH;

        // 4. Büyüteç kutusunun etrafında ne kadar alan göstereceğimizi belirle.
        //    zoomFactor = "1 kaynak pikseli, büyüteçte kaç piksel olarak görünsün?"
        const double zoomFactor = 4.0;
        double cropW = magnifierGrid.Bounds.Width / zoomFactor;
        double cropH = magnifierGrid.Bounds.Height / zoomFactor;

        // Kırpma alanı resmin sınırlarını taşmasın
        cropW = Math.Min(cropW, imgPixelW);
        cropH = Math.Min(cropH, imgPixelH);

        double left = imagePixelX - (cropW / 2.0);
        double top = imagePixelY - (cropH / 2.0);
        left = Math.Clamp(left, 0, Math.Max(0, imgPixelW - cropW));
        top = Math.Clamp(top, 0, Math.Max(0, imgPixelH - cropH));

        int cropX = (int)Math.Round(left);
        int cropY = (int)Math.Round(top);
        int cropWidth = (int)Math.Round(cropW);
        int cropHeight = (int)Math.Round(cropH);

        // Güvenlik: sıfır veya negatif boyutta kırpma yapmaya çalışma
        if (cropWidth > 0 && cropHeight > 0 &&
            cropX + cropWidth <= imgPixelW && cropY + cropHeight <= imgPixelH)
        {
            var sourceRect = new Avalonia.PixelRect(cropX, cropY, cropWidth, cropHeight);
            magnifierImage.Source = new CroppedBitmap(vmMag.GraphImage, sourceRect);

            // ==========================================
            // 🔴 KIRMIZI NOKTALARI / KALİBRASYON İŞARETLERİNİ DE BÜYÜTEÇTE GÖSTER
            // ==========================================
            // [YENİ]: Büyüteç şimdiye kadar sadece resmi gösteriyordu, üstüne çizdiğin
            // noktaları (Ellipse'ler DrawingCanvas'ın çocukları) göstermiyordu.
            // Burada: kırpma alanının (cropX,cropY,cropWidth,cropHeight) canvas-piksel
            // karşılığını buluyoruz, o alanın içine düşen her noktayı küçültülmüş
            // boyutta MagnifierOverlayCanvas üzerine yeniden çiziyoruz.

            var overlayCanvas = this.FindControl<Canvas>("MagnifierOverlayCanvas");
            if (overlayCanvas != null)
            {
                overlayCanvas.Children.Clear();

                // Kırpma alanının (image-piksel) canvas-piksel (0-800/0-600) karşılığı
                double cropCanvasLeft = offsetX + (cropX * uniformScale);
                double cropCanvasTop = offsetY + (cropY * uniformScale);
                double cropCanvasWidth = cropWidth * uniformScale;
                double cropCanvasHeight = cropHeight * uniformScale;

                // 1 canvas-pikseli, büyüteç panelinde kaç piksele denk geliyor?
                double overlayScale = zoomFactor / uniformScale;

                foreach (var child in DrawingCanvas.Children)
                {
                    if (child is not Ellipse dot) continue;

                    double dotCenterX = Canvas.GetLeft(dot) + (dot.Width / 2);
                    double dotCenterY = Canvas.GetTop(dot) + (dot.Height / 2);

                    bool isInsideCrop =
                        dotCenterX >= cropCanvasLeft && dotCenterX <= cropCanvasLeft + cropCanvasWidth &&
                        dotCenterY >= cropCanvasTop && dotCenterY <= cropCanvasTop + cropCanvasHeight;

                    if (!isInsideCrop) continue;

                    double panelX = (dotCenterX - cropCanvasLeft) * overlayScale;
                    double panelY = (dotCenterY - cropCanvasTop) * overlayScale;
                    double miniWidth = Math.Max(2, dot.Width * overlayScale);
                    double miniHeight = Math.Max(2, dot.Height * overlayScale);

                    var miniDot = new Ellipse
                    {
                        Width = miniWidth,
                        Height = miniHeight,
                        Fill = dot.Fill,
                        Stroke = dot.Stroke,
                        StrokeThickness = dot.StrokeThickness
                    };
                    Canvas.SetLeft(miniDot, panelX - (miniWidth / 2));
                    Canvas.SetTop(miniDot, panelY - (miniHeight / 2));
                    overlayCanvas.Children.Add(miniDot);
                }
            }
        }
    }

    }

    public void DrawModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (!vm.Converter.IsCalibrated)
            {
                vm.SystemStatus = "Önce kalibrasyonu tamamlamalısınız!";
                return;
            }
        
            _isDrawModeActive = !_isDrawModeActive;
            if (_isDrawModeActive) { _isSingleAddModeActive = false; _isDeleteModeActive = false; } // Mod çakışması önlendi

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

            DrawingCanvas.Children.Clear();
            _calibrationMarkers.Clear();
            _drawnDataDots.Clear();
            vm.LiveDataPoints.Clear();
            vm._currentOrderIndex = 1;

            _calibrationClicksCount = 0;
            _activeCalibrationStep = "";
            _isDrawModeActive = false;
            _isSingleAddModeActive = false;
            _isDeleteModeActive = false; // Sıfırlanırken silme modu da kapanır
            
            vm.SystemStatus = "🔄 Tüm kalibrasyon ve grafik verileri başarıyla sıfırlandı.";
        }
    }

    // ====================================================================
    // [2. ÖZELLİK + FULL ENTEGRE]: HEM GERİ AL (UNDO) HEM SEÇEREK SİL (AVCI MODU)
    // ====================================================================
    public void TekDeletePointButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // 1. ÖZELLİK: ANINDA GERİ AL (CLASSIC UNDO)
            // Butona basıldığı an ekrandaki EN SON NOKTAYI anında sil!
            if (_drawnDataDots.Count > 0 && vm.LiveDataPoints.Count > 0)
            {
                int lastIndex = _drawnDataDots.Count - 1; // Listenin en son elemanı (Stack.Top)
                var lastDot = _drawnDataDots[lastIndex];
                
                DrawingCanvas.Children.Remove(lastDot); // Ekrandan görseli kaldır
                _drawnDataDots.RemoveAt(lastIndex);     // Listeden son elemanı at (Pop işlevi)

                // Backend listesinden de son noktayı temizle
                vm.LiveDataPoints.RemoveAt(vm.LiveDataPoints.Count - 1);
                vm._currentOrderIndex--;
            }
        }
    }

    public void DeletePointButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            

            // 2. ÖZELLİK: AVCI MODUNU (SEÇEREK SİLME) AÇ / KAPAT
            _isDeleteModeActive = !_isDeleteModeActive;
            
            // Çakışma olmasın diye diğer çizim modlarını kapatıyoruz
            if (_isDeleteModeActive) { _isDrawModeActive = false; _isSingleAddModeActive = false; }

            vm.SystemStatus = _isDeleteModeActive 
                ? $"↩️ Son nokta geri alındı! 🎯 SİLME MODU AKTİF: Eski noktalara da tıklayarak silebilirsiniz." 
                : $"↩️ Son nokta geri alındı. ✏️ Silme Modu kapatıldı.";
        }
    }

    public void AddPointButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (!vm.Converter.IsCalibrated)
            {
                vm.SystemStatus = "⚠️ Önce kalibrasyonu tamamlamalısınız!";
                return;
            }

            _isSingleAddModeActive = !_isSingleAddModeActive;
            if (_isSingleAddModeActive) { _isDrawModeActive = false; _isDeleteModeActive = false; } // Mod çakışması önlendi

            vm.SystemStatus = _isSingleAddModeActive 
                ? "📍 Tek Nokta Ekleme AKTİF. Resme tıklayarak hassas nokta ekleyebilirsiniz." 
                : "📍 Tek Nokta Ekleme KAPATILDI.";
        }
    }
    public void ClearAllPointsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // 1. Senin yazdığın sayacı (OrderIndex) 1'e çeken ve matematiksel listeyi boşaltan metodu çağır
            vm.ClearStreamData();

            // 2. Ekrandaki tüm kırmızı/sarı noktaları söküp at (Foreach ile dolaşıp Canvas'tan siliyoruz)
            foreach (var dot in _drawnDataDots)
            {
                DrawingCanvas.Children.Remove(dot);
            }

            // 3. Görsel Listeyi tek hamlede tertemiz yap (Pop kullanmaya gerek kalmadı!)
            _drawnDataDots.Clear();

            // 4. Kullanıcıya sayacın sıfırlandığını arayüzde hissettir
            vm.SystemStatus = $"🧹 Tüm veri noktaları temizlendi. Sayaç başa alındı! (Mevcut Nokta: {vm.LiveDataPoints.Count})";
        }
    }
    public void ZoomInButton_Click(object? sender, RoutedEventArgs e)
    {
        // 🚀 KURŞUN GEÇİRMEZ YÖNTEM: "Git XAML içinden MainZoomGrid'i zorla bul!"
        var mainZoomGrid = this.FindControl<Grid>("MainZoomGrid");
        
        if (mainZoomGrid?.RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX += 0.2; 
            scale.ScaleY += 0.2;
        }
    }

    public void ZoomOutButton_Click(object? sender, RoutedEventArgs e)
    {
        var mainZoomGrid = this.FindControl<Grid>("MainZoomGrid");

        if (mainZoomGrid?.RenderTransform is ScaleTransform scale)
        {
            if (scale.ScaleX > 0.4) 
            {
                scale.ScaleX -= 0.2; 
                scale.ScaleY -= 0.2;
            }
        }
    }
    
}