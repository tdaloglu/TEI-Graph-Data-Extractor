using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media; 
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using System;
using TEIGraphDataExtractor.Models;
using TEIGraphDataExtractor.ViewModels;
using TEIGraphDataExtractor.Services.Export;


namespace TEIGraphDataExtractor.Views;

public partial class MainWindow : Window
{
    private string _activeCalibrationStep = "";
    private int _calibrationClicksCount = 0;
    private bool _isDrawModeActive = false;
    private System.Collections.Generic.Dictionary<string, Ellipse> _calibrationMarkers = new();
    private System.Collections.Generic.Dictionary<string, TextBlock> _calibrationLabels = new();
    private static readonly string[] _calibrationOrder = { "X1", "X2", "Y1", "Y2" };
    private Avalonia.Point _lastCollectedPoint = new Avalonia.Point(0, 0);

    // Görsel noktaları tuttuğumuz liste
    private System.Collections.Generic.List<Ellipse> _drawnDataDots = new();

    private bool _isSingleAddModeActive = false; 
    private bool _isDeleteModeActive = false;    
    private bool _isAdjustModeActive = false;
    private Ellipse? _draggedDot = null; 
    private IBrush _currentPenColor = Brushes.Red;

    public MainWindow()
    {
        InitializeComponent();
        this.AddHandler(KeyDownEvent, Window_KeyDown, RoutingStrategies.Tunnel);
    }

    public async void LoadImageButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
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
                SetCalibrationStep("X1", "📍 Resim üzerinde X1 (Min) noktasını tıklayarak seçin...");
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
        var point = e.GetPosition(DrawingCanvas);

        // ==========================================
        // SİLME MODU (AVCI)
        // ==========================================
        if (_isDeleteModeActive && DataContext is MainWindowViewModel vmDelete)
        {
            Ellipse? closestDot = null;
            double minDist = 15.0; 

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

            if (closestDot != null)
            {
                int targetIndex = _drawnDataDots.IndexOf(closestDot);

                DrawingCanvas.Children.Remove(closestDot);
                _drawnDataDots.RemoveAt(targetIndex);

                if (targetIndex >= 0 && targetIndex < vmDelete.LiveDataPoints.Count)
                {
                    vmDelete.LiveDataPoints.RemoveAt(targetIndex);
                }

                vmDelete.SystemStatus = $"🎯 Nokta seçilerek silindi. Kalan Nokta: {vmDelete.LiveDataPoints.Count}";
            }
            else
            {
                vmDelete.SystemStatus = "ℹ️ Tıkladığınız yerde silinecek bir nokta bulunamadı.";
            }
            return; 
        }

        // ==========================================
        // TAŞIMA MODU
        // ==========================================
        if (_isAdjustModeActive)
        {
            Ellipse? closestDot = null;
            double minDist = 15.0; 

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

            if (closestDot != null)
            {
                _draggedDot = closestDot;
                e.Pointer.Capture(DrawingCanvas);
            }
            return;
        }

        // ==========================================
        // KALİBRASYON VE TEK NOKTA EKLEME MODU
        // ==========================================
        if (DataContext is MainWindowViewModel vm)
        {
            if (!string.IsNullOrEmpty(_activeCalibrationStep))
            {
                string currentStep = _activeCalibrationStep;

                switch (currentStep)
                {
                    case "X1": vm.MinPixelX = point.X; break;
                    case "X2": vm.XMaxPixelX = point.X; break;
                    case "Y1": vm.MinPixelY = point.Y; break;
                    case "Y2": vm.YMaxPixelY = point.Y; break;
                }

                if (_calibrationMarkers.ContainsKey(currentStep))
                {
                    DrawingCanvas.Children.Remove(_calibrationMarkers[currentStep]);
                }
                if (_calibrationLabels.ContainsKey(currentStep))
                {
                    DrawingCanvas.Children.Remove(_calibrationLabels[currentStep]);
                }

                IBrush markerColor = currentStep switch
                {
                    "X1" => Brushes.Cyan,         
                    "X2" => Brushes.DarkOrange,   
                    "Y1" => Brushes.DodgerBlue,   
                    "Y2" => Brushes.LimeGreen,    
                    _ => Brushes.White
                };

                var marker = new Ellipse { Width = 10, Height = 10, Fill = markerColor, Stroke = Brushes.White, StrokeThickness = 2 };
                Canvas.SetLeft(marker, point.X - 5);
                Canvas.SetTop(marker, point.Y - 5);
                DrawingCanvas.Children.Add(marker);
                _calibrationMarkers[currentStep] = marker;

                var label = new TextBlock
                {
                    Text = currentStep, Foreground = markerColor, FontWeight = Avalonia.Media.FontWeight.Bold,
                    FontSize = 13, Background = new SolidColorBrush(Color.FromArgb(160, 7, 11, 20))
                };
                Canvas.SetLeft(label, point.X + 8);
                Canvas.SetTop(label, point.Y - 18);
                DrawingCanvas.Children.Add(label);
                _calibrationLabels[currentStep] = label;

                vm.SystemStatus = $"✅ {currentStep} Noktası Güncellendi ({point.X:F0}px, {point.Y:F0}px).";
                _activeCalibrationStep = ""; 
                _calibrationClicksCount++;

                if (_calibrationClicksCount >= 4)
                {
                    vm.SystemStatus = "4 kalibrasyon noktası başarı ile seçildi. 'Kalibrasyonu Tamamla' butonuna basabilirsiniz.";
                    _calibrationClicksCount = 0;

                    foreach (var kvp in _calibrationLabels)
                    {
                        DrawingCanvas.Children.Remove(kvp.Value);
                    }
                    _calibrationLabels.Clear();
                }
                else
                {
                    string? nextStep = null;
                    foreach (var step in _calibrationOrder)
                    {
                        if (!_calibrationMarkers.ContainsKey(step))
                        {
                            nextStep = step;
                            break;
                        }
                    }

                    if (nextStep != null)
                    {
                        string message = nextStep switch
                        {
                            "X1" => "📍 Şimdi X1 (Min) noktasını tıklayarak seçin...",
                            "X2" => "📍 Şimdi X2 (Max) noktasını tıklayarak seçin...",
                            "Y1" => "📍 Şimdi Y1 (Min) noktasını tıklayarak seçin...",
                            "Y2" => "📍 Şimdi Y2 (Max) noktasını tıklayarak seçin...",
                            _ => "📍 Sıradaki noktayı tıklayarak seçin..."
                        };
                        SetCalibrationStep(nextStep, $"✅ {currentStep} kaydedildi. {message}");
                    }
                }
                return;
            }

            if (_isSingleAddModeActive && string.IsNullOrEmpty(_activeCalibrationStep))
            {
                var newPoint = vm.CaptureStreamPoint(point.X, point.Y);
                if (newPoint != null)
                {
                    var dataDot = new Ellipse { Width = 4, Height = 4, Fill = _currentPenColor };
                    Canvas.SetLeft(dataDot, point.X - 2);
                    Canvas.SetTop(dataDot, point.Y - 2);
                    DrawingCanvas.Children.Add(dataDot); 
                    _drawnDataDots.Add(dataDot);         

                    vm.SystemStatus = $"📍 Nokta eklendi: X={newPoint.XValue:F3}, Y={newPoint.YValue:F3}";
                }
            }
        }
    } 

    public void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);

        // Nokta Sürükleme (Adjust Mode)
        if (_isAdjustModeActive && _draggedDot != null && e.GetCurrentPoint(DrawingCanvas).Properties.IsLeftButtonPressed)
        {
            Canvas.SetLeft(_draggedDot, point.X - (_draggedDot.Width / 2));
            Canvas.SetTop(_draggedDot, point.Y - (_draggedDot.Height / 2));

            int dotIndex = _drawnDataDots.IndexOf(_draggedDot);

            if(dotIndex >= 0 && DataContext is MainWindowViewModel vmMove && vmMove.Converter.IsCalibrated)
            {
                var realCoords = vmMove.Converter.PixelToRealWorld(point.X, point.Y);
                vmMove.LiveDataPoints[dotIndex].XValue = realCoords.RealX;
                vmMove.LiveDataPoints[dotIndex].YValue = realCoords.RealY;
                CoordinateText.Text = $"Taşınıyor... X: {realCoords.RealX:F3} | Y: {realCoords.RealY:F3}";
            }
            DrawingCanvas.InvalidateVisual();
        }

        // Çizim (Draw Mode)
        if (DataContext is MainWindowViewModel vm && vm.Converter.IsCalibrated)
        {
            if (vm.ZGroups.Count == 0)
            {
                vm.SystemStatus = "⚠️ UYARI: Hiçbir Z grubu bulunmuyor! Çizim yapabilmek için lütfen en az 1 grup ekleyin.";
                _isDrawModeActive = false;
                return;
            }
            var realCoords = vm.Converter.PixelToRealWorld(point.X, point.Y);
            CoordinateText.Text = $"Gerçek X: {realCoords.RealX:F3} | Gerçek Y: {realCoords.RealY:F3}";

            var properties = e.GetCurrentPoint(DrawingCanvas).Properties;

            if (_isDrawModeActive && !_isDeleteModeActive && properties.IsLeftButtonPressed)
            {
                double distance = Math.Sqrt(Math.Pow(point.X - _lastCollectedPoint.X, 2) + Math.Pow(point.Y - _lastCollectedPoint.Y, 2));
                if (distance >= 5)
                {
                    var newPoint = vm.CaptureStreamPoint(point.X, point.Y);
                    if (newPoint != null)
                    {
                        var dataDot = new Ellipse { Width = 4, Height = 4, Fill =_currentPenColor  };
                        Canvas.SetLeft(dataDot, point.X - 2);
                        Canvas.SetTop(dataDot, point.Y - 2);

                        DrawingCanvas.Children.Add(dataDot);
                        _drawnDataDots.Add(dataDot); 
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
        // BÜYÜTEÇ MOTORU (CROP TABANLI)
        // ==========================================
        var magnifierImage = this.FindControl<Image>("MagnifierImage");
        var magnifierGrid = this.FindControl<Grid>("MagnifierGrid");

        if (DataContext is MainWindowViewModel vmMag &&
            magnifierImage != null && magnifierGrid != null &&
            DrawingCanvas.Bounds.Width > 0 && DrawingCanvas.Bounds.Height > 0 &&
            vmMag.GraphImage != null && magnifierGrid.Bounds.Width > 0 && magnifierGrid.Bounds.Height > 0)
        {
            double imgPixelW = vmMag.GraphImage.PixelSize.Width;
            double imgPixelH = vmMag.GraphImage.PixelSize.Height;

            double boxW = DrawingCanvas.Bounds.Width;   
            double boxH = DrawingCanvas.Bounds.Height;  

            double uniformScale = Math.Min(boxW / imgPixelW, boxH / imgPixelH);
            double renderedW = imgPixelW * uniformScale;
            double renderedH = imgPixelH * uniformScale;
            double offsetX = (boxW - renderedW) / 2.0;
            double offsetY = (boxH - renderedH) / 2.0;

            double percentX = (point.X - offsetX) / renderedW;
            double percentY = (point.Y - offsetY) / renderedH;
            percentX = Math.Clamp(percentX, 0.0, 1.0);
            percentY = Math.Clamp(percentY, 0.0, 1.0);

            double imagePixelX = percentX * imgPixelW;
            double imagePixelY = percentY * imgPixelH;

            const double zoomFactor = 4.0;
            double cropW = magnifierGrid.Bounds.Width / zoomFactor;
            double cropH = magnifierGrid.Bounds.Height / zoomFactor;

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

            if (cropWidth > 0 && cropHeight > 0 &&
                cropX + cropWidth <= imgPixelW && cropY + cropHeight <= imgPixelH)
            {
                var sourceRect = new Avalonia.PixelRect(cropX, cropY, cropWidth, cropHeight);
                magnifierImage.Source = new CroppedBitmap(vmMag.GraphImage, sourceRect);

                var overlayCanvas = this.FindControl<Canvas>("MagnifierOverlayCanvas");
                if (overlayCanvas != null)
                {
                    overlayCanvas.Children.Clear();

                    double cropCanvasLeft = offsetX + (cropX * uniformScale);
                    double cropCanvasTop = offsetY + (cropY * uniformScale);
                    double cropCanvasWidth = cropWidth * uniformScale;
                    double cropCanvasHeight = cropHeight * uniformScale;
                    double overlayScale = zoomFactor / uniformScale;

                    foreach (var child in DrawingCanvas.Children)
                    {
                        if (child is Ellipse dot)
                        {
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
                                Width = miniWidth, Height = miniHeight, Fill = dot.Fill,
                                Stroke = dot.Stroke, StrokeThickness = dot.StrokeThickness
                            };
                            Canvas.SetLeft(miniDot, panelX - (miniWidth / 2));
                            Canvas.SetTop(miniDot, panelY - (miniHeight / 2));
                            overlayCanvas.Children.Add(miniDot);
                        }
                        else if (child is TextBlock label)
                        {
                            double labelLeft = Canvas.GetLeft(label);
                            double labelTop = Canvas.GetTop(label);

                            bool isInsideCrop =
                                labelLeft >= cropCanvasLeft && labelLeft <= cropCanvasLeft + cropCanvasWidth &&
                                labelTop >= cropCanvasTop && labelTop <= cropCanvasTop + cropCanvasHeight;

                            if (!isInsideCrop) continue;

                            double panelX = (labelLeft - cropCanvasLeft) * overlayScale;
                            double panelY = (labelTop - cropCanvasTop) * overlayScale;

                            var miniLabel = new TextBlock
                            {
                                Text = label.Text, Foreground = label.Foreground,
                                Background = label.Background, FontWeight = label.FontWeight,
                                FontSize = Math.Max(8, label.FontSize * overlayScale)
                            };
                            Canvas.SetLeft(miniLabel, panelX);
                            Canvas.SetTop(miniLabel, panelY);
                            overlayCanvas.Children.Add(miniLabel);
                        }
                    }
                }
            }
        }
    }

    public void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedDot != null)
        {
            _draggedDot = null; 
            e.Pointer.Capture(null); 

            if (DataContext is MainWindowViewModel vm)
            {
                vm.SystemStatus = "✅ Nokta başarıyla yeni konumuna taşındı.";
            }
            DrawingCanvas.InvalidateVisual();
        }
    }

    // ==========================================
    // BUTON AKSİYONLARI (MOD GEÇİŞLERİ)
    // ==========================================

    public void DrawModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (!vm.Converter.IsCalibrated)
            {
                vm.SystemStatus = "Önce kalibrasyonu tamamlamalısınız!";
                _isDrawModeActive = false;
                return;
            }

            if (vm.ZGroups.Count == 0)
            {
                vm.SystemStatus = "⚠️ UYARI: Hiçbir Z grubu bulunmuyor! Çizim yapabilmek için lütfen en az 1 grup ekleyin.";
                _isDrawModeActive = false;
                return;
            }

            _isDrawModeActive = true;
            if (_isDrawModeActive) { _isSingleAddModeActive = false; _isDeleteModeActive = false; _isAdjustModeActive = false; } 

            vm.SystemStatus = _isDrawModeActive
                ? "✒️ Kalem Modu AKTİF. Farenin sol tuşuna basılı tutarak çizim yapın."
                : "✒️ Kalem Modu KAPATILDI.";
        }
    }

    public void AddPointButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (!vm.Converter.IsCalibrated)
            {
                vm.SystemStatus = "⚠️ Önce kalibrasyonu tamamlamalısınız!";
                _isSingleAddModeActive = false;
                return;
            }

            if (vm.ZGroups.Count == 0)
            {
                vm.SystemStatus = "⚠️ UYARI: Hiçbir Z grubu bulunmuyor! Çizim yapabilmek için lütfen en az 1 grup ekleyin.";
                _isDrawModeActive = false;
                _isSingleAddModeActive = false;
                return;
            }

            _isSingleAddModeActive = true;
            if (_isSingleAddModeActive) { _isAdjustModeActive = false; _isDrawModeActive = false; _isDeleteModeActive = false; } 

            vm.SystemStatus = _isSingleAddModeActive
                ? "📍 Tek Nokta Ekleme AKTİF. Resme tıklayarak hassas nokta ekleyebilirsiniz."
                : "📍 Tek Nokta Ekleme KAPATILDI.";
        }
    }

    public void AdjustModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _isAdjustModeActive = true;
            if (_isAdjustModeActive) { _isDrawModeActive = false; _isDeleteModeActive = false; _isSingleAddModeActive = false; }
            
            vm.SystemStatus = _isAdjustModeActive 
                ? "🖐️ TAŞIMA MODU AKTİF: Yanlış noktaları farenizle sürükleyip düzeltebilirsiniz." 
                : "🖐️ Taşıma Modu kapatıldı.";
        }
    }

    public void DeletePointButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            _isDeleteModeActive = true;
            if (_isDeleteModeActive) { _isDrawModeActive = false; _isSingleAddModeActive = false; _isAdjustModeActive = false; }

            vm.SystemStatus = _isDeleteModeActive
                ? "🎯 SİLME MODU AKTİF: Noktalara tıklayarak silebilirsiniz."
                : "✏️ Silme Modu kapatıldı.";
        }
    }

    public void TekDeletePointButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (_drawnDataDots.Count > 0 && vm.LiveDataPoints.Count > 0)
            {
                int lastIndex = _drawnDataDots.Count - 1;
                var lastDot = _drawnDataDots[lastIndex];

                DrawingCanvas.Children.Remove(lastDot);
                _drawnDataDots.RemoveAt(lastIndex);

                vm.LiveDataPoints.RemoveAt(vm.LiveDataPoints.Count - 1);
                vm._currentOrderIndex--;

                vm.SystemStatus = $"↩️ Son nokta geri alındı. Kalan nokta: {vm.LiveDataPoints.Count}";
            }
            else
            {
                vm.SystemStatus = "ℹ️ Geri alınacak nokta yok.";
            }
        }
    }

    public void ClearAllPointsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ClearStreamData();
            foreach (var dot in _drawnDataDots)
            {
                DrawingCanvas.Children.Remove(dot);
            }
            _drawnDataDots.Clear();
            vm.SystemStatus = $"🧹 Tüm veri noktaları temizlendi. Sayaç başa alındı! (Mevcut Nokta: {vm.LiveDataPoints.Count})";
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
            vm.RealXMin = "0"; vm.RealXMax = "0";
            vm.RealYMin = "0"; vm.RealYMax = "0";
            vm.MinPixelX = 0.0; vm.MinPixelY = 0.0;
            vm.XMaxPixelX = 0.0; vm.YMaxPixelY = 0.0;

            DrawingCanvas.Children.Clear();
            _calibrationMarkers.Clear();
            _calibrationLabels.Clear();
            _drawnDataDots.Clear();
            vm.LiveDataPoints.Clear();
            vm._currentOrderIndex = 1;

            _calibrationClicksCount = 0;
            _activeCalibrationStep = "";
            _isDrawModeActive = false;
            _isSingleAddModeActive = false;
            _isDeleteModeActive = false; 
            _isAdjustModeActive = false;

            SetCalibrationStep("X1", "🔄 Tüm kalibrasyon ve grafik verileri sıfırlandı. 📍 X1 (Min) noktasını tıklayarak seçin...");
        }
    }

    public void SelectZGroupButton_Click(object? sender, RoutedEventArgs e)
    {
        // 1. Tag kontrolü ve ZGroupItem dönüşümü
        if (sender is Button btn && btn.Tag is TEIGraphDataExtractor.Models.ZGroupItem clickedGroup)
        {
            // 2. DataContext'in ViewModel olduğunu doğruluyoruz ve 'vm' değişkenini yaratıyoruz
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SetActiveGroup(clickedGroup);

                if (!string.IsNullOrEmpty(clickedGroup.ColorHex))
                {
                    try
                    {
                        _currentPenColor = Brush.Parse(clickedGroup.ColorHex);
                        vm.SystemStatus = $"🎨 Aktif Grup Değişti: Z = {clickedGroup.ZValue}. Kalem rengi güncellendi!";
                    }
                    catch
                    {
                        _currentPenColor = Brushes.Red; // Dönüşüm patlarsa kırmızıya dön
                    }
                }
            } 
        }
    }

    public void ViewDataButton_Click(object? sender, RoutedEventArgs e)
    {
        var tabControl = this.FindControl<TabControl>("RightTabControl");
        if (tabControl != null)
        {
            tabControl.SelectedIndex = 1; 
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SystemStatus = "📊 Veri tablosu görüntülendi.";
            }
        }
    }

    public async void ExportCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "TEI Grafik Verisini CSV Olarak Kaydet",
                DefaultExtension = "csv",
                SuggestedFileName = $"TEI_Grafik_Verisi_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Dosyası (*.csv)") {Patterns = new[] {"*.csv"}}
                }   
            });

            if (file != null)
            {
                vm.ExportToCsv(file.Path.LocalPath);
            }
        }
    }

    // ==========================================
    // ZOOM MOTORU (LAYOUT TRANSFORM UYUMLU)
    // ==========================================

    public void ZoomInButton_Click(object? sender, RoutedEventArgs e)
    {
        AdjustZoom(0.2);
    }

    public void ZoomOutButton_Click(object? sender, RoutedEventArgs e)
    {
        AdjustZoom(-0.2);
    }

    public void MainZoomScrollViewer_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var keyModifiers = e.KeyModifiers;
        bool isZoomGesture = keyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);

        if (isZoomGesture)
        {
            double delta = e.Delta.Y > 0 ? 0.2 : -0.2;
            AdjustZoom(delta);
            e.Handled = true; 
        }
    }

    private void AdjustZoom(double delta)
    {
        var zoomControl = this.FindControl<LayoutTransformControl>("ZoomTransformControl");
        var mainZoomScrollViewer = this.FindControl<ScrollViewer>("MainZoomScrollViewer"); 

        if (zoomControl?.LayoutTransform is ScaleTransform scale)
        {
            double newScale = scale.ScaleX + delta;
            newScale = Math.Clamp(newScale, 0.4, 5.0);
            
            scale.ScaleX = newScale;
            scale.ScaleY = newScale;

            if (mainZoomScrollViewer != null)
            {
                if (newScale > 1.0)
                {
                    mainZoomScrollViewer.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
                    mainZoomScrollViewer.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
                }
                else
                {
                    mainZoomScrollViewer.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
                    mainZoomScrollViewer.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
                }
            }
            
        }
    }

    public void DeleteZGroupButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ZGroupItem clickedGroup)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.RemoveZGroup(clickedGroup);
            }
        }
    }
    // [GÜNCELLENDİ]: Hem Windows (Ctrl+Z) hem Mac (Cmd+Z) destekleyen geri al fonksiyonu
    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
       
       bool isCtrlOrCmdPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                           e.KeyModifiers.HasFlag(KeyModifiers.Meta);
    if (isCtrlOrCmdPressed && e.Key == Key.Z)
    {
        TekDeletePointButton_Click(sender, new Avalonia.Interactivity.RoutedEventArgs());
        e.Handled = true;
    }

    }
}