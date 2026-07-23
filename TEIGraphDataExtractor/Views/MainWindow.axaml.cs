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
using Avalonia.Platform;
using Avalonia;
using System.Linq;
using System.Linq.Expressions;


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

        this.DataContextChanged += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.LiveDataPoints.CollectionChanged += (sender, args) =>
                {
                    for (int i = _drawnDataDots.Count - 1; i >= 0; i--)
                    {
                        if (_drawnDataDots[i].Tag is DataPoint pt && !vm.LiveDataPoints.Contains(pt))
                        {
                            DrawingCanvas.Children.Remove(_drawnDataDots[i]);
                            _drawnDataDots.RemoveAt(i);
                        }
                    }
                };
            }
        };
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

                // [KİLİT ÇÖZÜM]: Eğer önceki oturumdan gelen kalibrasyon hafızası varsa X1-X2 SEÇTİRMEYE ZORLAMA!
                if (vm.MinPixelX != 0 || vm.XMaxPixelX != 0 || vm.Converter.IsCalibrated)
                {
                    _activeCalibrationStep = ""; // Kalibrasyon modunu tamamen kapat!
                    vm.SystemStatus = "📂 Önceki oturum kalibrasyonu aktif! Doğrudan çizime veya düzenlemeye devam edebilirsiniz.";
                }
                else
                {
                    // Sıfırdan başlanıyorsa X1 seçtir
                    SetCalibrationStep("X1", "📍 Resim üzerinde X1 (Min) noktasını tıklayarak seçin...");
                }

                // Resim tuvale tam yerleşene kadar 150 milisaniye bekle ve eski noktaları ekrana çiz!
                await System.Threading.Tasks.Task.Delay(150);
                RedrawMemoryPoints();
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
        if (e.GetCurrentPoint(DrawingCanvas).Properties.IsLeftButtonPressed && DataContext is MainWindowViewModel vmStart)
        {
            vmStart.StartDrawingStroke();
        }
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
                if (vm.ZGroups.Count == 0)
                {
                    vm.SystemStatus = "⚠️ UYARI: Hiçbir Z grubu bulunmuyor! Lütfen önce en az 1 grup ekleyin.";
                    return;
                }
                var newPoint = vm.CaptureStreamPoint(point.X, point.Y);
                if (newPoint != null)
                {
                    var dataDot = new Ellipse { Width = 4, Height = 4, Fill = _currentPenColor, Tag = newPoint};
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
                vm.TriggerNoGroupWarning();
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
                        var dataDot = new Ellipse { Width = 4, Height = 4, Fill =_currentPenColor , Tag = newPoint};
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
        if (DataContext is MainWindowViewModel vmEnd)
        {
            vmEnd.EndDrawingStroke();
        }

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
            if (vm.GraphImage == null) 
            {
                vm.SystemStatus = "⚠️ UYARI: Lütfen önce bir resim ekleyiniz!";
                _isDrawModeActive = false;
                vm.TriggerNoImageWarning(); 
                return; // Kodu kes
            }
            if (!vm.Converter.IsCalibrated)
            {
                vm.SystemStatus = "Önce kalibrasyonu tamamlamalısınız!";
                _isDrawModeActive = false;
                vm.TriggerCalibrationWarning();
                return;
            }
            

            if (vm.ZGroups.Count == 0)
            {
                vm.SystemStatus = "⚠️ UYARI: Hiçbir Z grubu bulunmuyor! Çizim yapabilmek için lütfen en az 1 grup ekleyin.";
                _isDrawModeActive = false;
                vm.TriggerNoGroupWarning();
                return;
            }
            if (!vm.HasSelectedGroup)
            {
                vm.SystemStatus = "⚠️ UYARI: Lütfen önce bir grup seçiniz!";
                _isDrawModeActive = false;
                vm.TriggerGroupWarning(); // Uyarı penceresini ve zamanlayıcıyı tetikle
                return;
            }

            _isDrawModeActive = true;
            if (_isDrawModeActive) { _isSingleAddModeActive = false; _isDeleteModeActive = false; _isAdjustModeActive = false; } 

            vm.SystemStatus = _isDrawModeActive
                ? "✒️ Kalem Modu AKTİF. Farenin sol tuşuna basılı tutarak çizim yapın."
                : "✒️ Kalem Modu KAPATILDI.";

            UpdateCursor();
        }
        
    }

    public void AddPointButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.GraphImage == null) 
            {
                vm.SystemStatus = "⚠️ UYARI: Lütfen önce bir resim ekleyiniz!";
                _isDrawModeActive = false;
                vm.TriggerNoImageWarning(); // Büyük pop-up'ı tetikle
                return; // Kodu kes
            }
            if (!vm.Converter.IsCalibrated)
            {
                vm.SystemStatus = "⚠️ Önce kalibrasyonu tamamlamalısınız!";
                _isSingleAddModeActive = false;
                vm.TriggerCalibrationWarning();
                return;
            }

            if (vm.ZGroups.Count == 0)
            {
                vm.SystemStatus = "⚠️ UYARI: Hiçbir Z grubu bulunmuyor! Çizim yapabilmek için lütfen en az 1 grup ekleyin.";
                _isDrawModeActive = false;
                _isSingleAddModeActive = false;
                vm.TriggerNoGroupWarning();
                return;
            }
            if (!vm.HasSelectedGroup)
            {
                vm.SystemStatus = "⚠️ UYARI: Lütfen önce bir grup seçiniz!";
                _isSingleAddModeActive = false;
                vm.TriggerGroupWarning(); // Uyarı penceresini ve zamanlayıcıyı tetikle
                return;
            }

            _isSingleAddModeActive = true;
            if (_isSingleAddModeActive) { _isAdjustModeActive = false; _isDrawModeActive = false; _isDeleteModeActive = false; } 

            vm.SystemStatus = _isSingleAddModeActive
                ? "📍 Tek Nokta Ekleme AKTİF. Resme tıklayarak hassas nokta ekleyebilirsiniz."
                : "📍 Tek Nokta Ekleme KAPATILDI.";
        }
        UpdateCursor();
    }

    public void AdjustModeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.ZGroups.Count == 0)
            {
                vm.SystemStatus = "⚠️ UYARI: Hiçbir Z grubu bulunmuyor! Düzenleme yapmak için grup bulunmalıdır.";
                _isAdjustModeActive = false;
                return;
            }
            _isAdjustModeActive = true;
            if (_isAdjustModeActive) { _isDrawModeActive = false; _isDeleteModeActive = false; _isSingleAddModeActive = false; }
            
            vm.SystemStatus = _isAdjustModeActive 
                ? "🖐️ TAŞIMA MODU AKTİF: Yanlış noktaları farenizle sürükleyip düzeltebilirsiniz." 
                : "🖐️ Taşıma Modu kapatıldı.";
        }
        UpdateCursor();
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
        UpdateCursor();
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
        UpdateCursor();
    }

    public void UndoLastStrokeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.UndoLastCurve();
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
            if (success)
            {
                RedrawMemoryPoints();
            }
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

                for (int i = _drawnDataDots.Count - 1; i >= 0; i--)
                {
                    var dot = _drawnDataDots[i];

                    if (dot.Tag is DataPoint pt && !vm.LiveDataPoints.Contains(pt))
                    {
                        DrawingCanvas.Children.Remove(dot);
                        _drawnDataDots.RemoveAt(i);
                    }
                }
            }
        }
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
       
       bool isCtrlOrCmdPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                           e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (isCtrlOrCmdPressed && e.Key == Key.Z)
        {
            TekDeletePointButton_Click(sender, new Avalonia.Interactivity.RoutedEventArgs());
            e.Handled = true;
        }

        if (isCtrlOrCmdPressed && e.Key == Key.S)
        {
            if (DataContext is MainWindowViewModel vmSave)
            {
                vmSave.SaveWorkspace();
            }
            e.Handled = true;
        }
    }

    // kullanıcı penceredeki tamama basarsa 10 saniyeyi beklemeden hemen kapat
    public void CloseWarningPopup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseWarning();
        }
    }
    public void CloseCalibrationWarningPopup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseCalibrationWarning();
        }
    }
    public void CloseNoGroupWarningPopup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseNoGroupWarning();
        }
    }
    public void CloseNoImageWarningPopup_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseNoImageWarning();
        }
    }



private Cursor? _customPenCursor; // İmleci hafızada tutmak için

private Cursor GetCustomPenCursor()
{
    // Eğer kalemi daha önce oluşturduysak tekrar tekrar oluşturmayıp eskisini veriyoruz (Performans için)
    if (_customPenCursor != null) return _customPenCursor;

    try
    {
        // 1. Senin gönderdiğin SVG Path verisini okuyoruz
        var geometry = Avalonia.Media.StreamGeometry.Parse("M7.5 2.75C7.5 2.33579 7.16421 2 6.75 2C6.33579 2 6 2.33579 6 2.75V5.75C6 6.60889 6.61875 7.32327 7.43481 7.47169L6.34113 10.4403C6.08209 11.1434 6.12117 11.9217 6.44935 12.5954L10.6068 21.129C10.8664 21.6619 11.4072 22 12 22C12.5927 22 13.1336 21.6619 13.3932 21.129L17.5034 12.6923C17.8691 11.9415 17.8739 11.0653 17.5165 10.3106L16.1851 7.5H16.25C17.2165 7.5 18 6.7165 18 5.75V2.75C18 2.33579 17.6642 2 17.25 2C16.8358 2 16.5 2.33579 16.5 2.75V5.75C16.5 5.88807 16.3881 6 16.25 6H7.75C7.61193 6 7.5 5.88807 7.5 5.75V2.75ZM14.5254 7.5L16.1609 10.9527C16.3234 11.2958 16.3212 11.6941 16.1549 12.0353L12.75 19.0243V12.2993C13.1984 12.04 13.5 11.5552 13.5 11C13.5 10.1716 12.8284 9.5 12 9.5C11.1716 9.5 10.5 10.1716 10.5 11C10.5 11.5552 10.8016 12.04 11.25 12.2993V19.0244L7.79783 11.9384C7.64866 11.6322 7.6309 11.2784 7.74864 10.9588L9.02294 7.5H14.5254Z");

        // 2. Bir Path kontrolü (şekil) oluşturuyoruz
        var path = new Path
        {
            Data = geometry,
            Fill = Avalonia.Media.Brushes.White, // Kalemin iç rengi
            Stroke = Avalonia.Media.Brushes.DeepSkyBlue, // Kalemin dış çizgisi (Uygulamanın mavi temasıyla uyumlu)
            StrokeThickness = 1,
            Width = 24,
            Height = 24
        };

        // 3. Arka planda sanal bir çizim yapıyoruz
        path.Measure(new Size(24, 24));
        path.Arrange(new Rect(0, 0, 24, 24));

        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(24, 24), new Vector(96, 96));
        rtb.Render(path);

        // 4. İŞTE SİHİR BURADA: Kalemin en sivri uç noktası tam olarak X:12, Y:22 pikseline denk geliyor!
        _customPenCursor = new Cursor(rtb, new PixelPoint(12, 22));
    }
    catch
    {
        // Bir hata olursa güvenlik önlemi olarak standart artı işaretini ver
        _customPenCursor = new Cursor(Avalonia.Input.StandardCursorType.Cross);
    }

    return _customPenCursor;
}

private Cursor? _customEraserCursor; // İmleci hafızada tutmak için

private Cursor GetCustomEraserCursor()
{
    // Eğer kalemi daha önce oluşturduysak tekrar tekrar oluşturmayıp eskisini veriyoruz (Performans için)
    if (_customEraserCursor != null) return _customEraserCursor;

    try
    {
        // 1. Senin gönderdiğin SVG Path verisini okuyoruz
        var geometry = Avalonia.Media.StreamGeometry.Parse("M20,2.25 C20.3796958,2.25 20.693491,2.53215388 20.7431534,2.89822944 L20.75,3 L20.75,17 C20.75,19.5504817 18.7398587,21.6314697 16.217428,21.7451121 L16,21.75 L8,21.75 C5.44951834,21.75 3.36853034,19.7398587 3.25488786,17.217428 L3.25,17 L3.25,3.50170761 C3.25,3.08749405 3.58578644,2.75170761 4,2.75170761 C4.37969577,2.75170761 4.69349096,3.03386149 4.74315338,3.39993706 L4.75,3.50170761 L4.75,6.791 L19.25,6.791 L19.25,3 C19.25,2.58578644 19.5857864,2.25 20,2.25 Z M19.25,13.5 L4.75,13.5 L4.75,17 C4.75,18.7330315 6.10645477,20.1492459 7.81557609,20.2448552 L8,20.25 L16,20.25 C17.7330315,20.25 19.1492459,18.8935452 19.2448552,17.1844239 L19.25,17 L19.25,13.5 Z M19.25,8.291 L4.75,8.291 L4.75,12 L19.25,12 L19.25,8.291");

        // 2. Bir Path kontrolü (şekil) oluşturuyoruz
        var path = new Path
        {
            Data = geometry,
            Fill = Avalonia.Media.Brushes.White, // Kalemin iç rengi
            Stroke = Avalonia.Media.Brushes.DeepSkyBlue, // Kalemin dış çizgisi (Uygulamanın mavi temasıyla uyumlu)
            StrokeThickness = 1,
            Width = 24,
            Height = 24
        };

        // 3. Arka planda sanal bir çizim yapıyoruz
        path.Measure(new Size(24, 24));
        path.Arrange(new Rect(0, 0, 24, 24));

        var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(24, 24), new Vector(96, 96));
        rtb.Render(path);

        // 4. İŞTE SİHİR BURADA: Kalemin en sivri uç noktası tam olarak X:12, Y:22 pikseline denk geliyor!
        _customEraserCursor = new Cursor(rtb, new PixelPoint(12, 22));
    }
    catch
    {
        // Bir hata olursa güvenlik önlemi olarak standart artı işaretini ver
        _customEraserCursor = new Cursor(Avalonia.Input.StandardCursorType.Cross);
    }

    return _customEraserCursor;
}

private void UpdateCursor()
{
    if (_isAdjustModeActive)
    {
        // Taşıma modu: El işareti
        DrawingCanvas.Cursor = new Cursor(StandardCursorType.DragMove);
    }
    else if (_isDrawModeActive)
    {
        DrawingCanvas.Cursor = GetCustomPenCursor();
    }
    else if (_isSingleAddModeActive)
    {
        DrawingCanvas.Cursor = new Cursor(StandardCursorType.Cross);
    }
    else if (_isDeleteModeActive)
    {
        DrawingCanvas.Cursor = GetCustomEraserCursor();
    }
    else
    {
        DrawingCanvas.Cursor = new Cursor(StandardCursorType.Arrow);
    }
}

    public void RedrawMemoryPoints()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.LiveDataPoints.Count == 0) return;

        foreach (var dot in _drawnDataDots)
        {
            DrawingCanvas.Children.Remove(dot);
        }
        _drawnDataDots.Clear();

        foreach (var pt in vm.LiveDataPoints) 
        {
            var pixelCoords = vm.Converter.RealWorldToPixel(pt.XValue, pt.YValue);

            var group = vm.ZGroups.FirstOrDefault(g => g.Id == pt.ZGroupId);
            IBrush dotColor = Brushes.Red;
            if (group != null && !string.IsNullOrEmpty(group.ColorHex))
            {
                try { dotColor = Brush.Parse(group.ColorHex);} catch { }
            }

            var dataDot = new Ellipse {Width = 4, Height = 4, Fill = dotColor, Tag = pt};
            Canvas.SetLeft(dataDot, pixelCoords.PixelX - 2);
            Canvas.SetTop(dataDot, pixelCoords.PixelY - 2);
            DrawingCanvas.Children.Add(dataDot);
            _drawnDataDots.Add(dataDot);
        }
        vm.SystemStatus = $"🚀 Harika! Önceki oturumdan kalan {vm.LiveDataPoints.Count} nokta tuvale çizildi.";
    }
}