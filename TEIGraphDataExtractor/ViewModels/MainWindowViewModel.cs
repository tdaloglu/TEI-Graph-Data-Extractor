using System;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using TEIGraphDataExtractor.Models;
using TEIGraphDataExtractor.Services;
using TEIGraphDataExtractor.Utils;
using TEIGraphDataExtractor.Services.Database;
using Avalonia.Diagnostics;


namespace TEIGraphDataExtractor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
   public CoordinateConverter Converter { get; } = new CoordinateConverter();
   private readonly GraphDataService _graphDataService = new GraphDataService();
   private string _systemStatus = "✅ Sistem Hazır (Kalibrasyon Bekleniyor)";
   public string SystemStatus
    {
        get => _systemStatus;
        set { _systemStatus = value; RaisePropertyChanged(); }
    }

    public Bitmap? _graphImage;
    public Bitmap? GraphImage
    {
        get => _graphImage;
        set { 
            _graphImage = value; 
            RaisePropertyChanged(); 
            RaisePropertyChanged(nameof(IsImageLoaded));
            }
    }
    public bool IsImageLoaded => GraphImage != null;
    private int _groupCount = 1;
    public int GroupCount
    {
        get => _groupCount;
        set { _groupCount = value; RaisePropertyChanged(); }
    }
    private double _realXMin = 0.0;
    public double RealXMin { get => _realXMin; set { _realXMin = value; RaisePropertyChanged(); } }

    private double _realXMax = 0.0;
    public double RealXMax { get => _realXMax; set { _realXMax = value; RaisePropertyChanged(); } }

    private double _realYMin = 0.0;
    public double RealYMin { get => _realYMin; set { _realYMin = value; RaisePropertyChanged(); } }

    private double _realYMax = 0.0;
    public double RealYMax { get => _realYMax; set { _realYMax = value; RaisePropertyChanged(); } }


    // --- PİKSEL DEĞERLERİNİ TUTACAK GEÇİCİ DEĞİŞKENLER ---
    public double MinPixelX { get; set; }
    public double MinPixelY { get; set; }
    public double XMaxPixelX { get; set; }
    public double YMaxPixelY { get; set; }

    //artık tüm noktalar secildiginde asagıdaki satırlar calısır

    private double _activeZValue = 0.8;
    public double ActiveZValue
    {
        get => _activeZValue;
        set {_activeZValue = value; RaisePropertyChanged(); }
    }

    private int _currentGraphId = 1;
    public int CurrentGraphId
    {
        get => _currentGraphId;
        set {_currentGraphId = value; RaisePropertyChanged(); }
    }

    public ObservableCollection<DataPoint> LiveDataPoints {get; } = new ObservableCollection<DataPoint>();

    public int _currentOrderIndex = 1;

    public bool TryCalibrate()
    {
        try
        {
            Converter.Calibrate(MinPixelX, XMaxPixelX, MinPixelY, YMaxPixelY, RealXMin, RealXMax, RealYMin, RealYMax);
            SystemStatus = "🚀 Kalibrasyon Başarılı! Farenizi gezdirip test edebilirsiniz.";
            return true;
        }

        catch (Exception ex)
        {
            SystemStatus = $"⚠️ Kalibrasyon Hatası: {ex.Message}";
            return false;
        }
    }

    public void StartDrawingStroke()
    {
        _graphDataService.BeginNewStroke();
    }

    public void EndDrawingStroke()
    {
        _graphDataService.EndCurrentStroke();

        SaveBatchToDatabase();
    }

    public DataPoint? CaptureStreamPoint(double pixelX, double pixelY)
    {
        if (!Converter.IsCalibrated)
        {
            Console.WriteLine("[UYARI] Çizim yapılmaya çalışıldı ama sistem henüz KALİBRE EDİLMEMİŞ!");
            return null;
        }

        try
        {
            var realCoord = Converter.PixelToRealWorld(pixelX, pixelY);

            var newPoint = new DataPoint
            {
                GraphId = CurrentGraphId,
                XValue = realCoord.RealX,
                YValue = realCoord.RealY,
                ZValue = ActiveZValue,
                OrderIndex = _currentOrderIndex++
            };

            _graphDataService.RegisterPoint(newPoint, LiveDataPoints);

            Console.WriteLine($"[NOKTA YAKALANDI] #{newPoint.OrderIndex} -> X: {newPoint.XValue}, Y: {newPoint.YValue}");

            return newPoint;
        }
        catch (Exception ex)
        {
            SystemStatus = $"⚠️ Tarama Hatası: {ex.Message}";
            return null;
        }
    }

    public void SaveBatchToDatabase()
    {
        if (LiveDataPoints.Count == 0) return;

        try
        {
            using var context = new AppDbContext();

            context.DataPoints.AddRange(LiveDataPoints);
            int savedCount = context.SaveChanges();

            _graphDataService.ClearHistory();

            SystemStatus = SystemStatus = $"💾 Başarılı: {savedCount} nokta veritabanına arşivlendi!";
        }
        catch (Exception ex)
        {
            SystemStatus = $"⚠️ Veritabanı Kayıt Hatası: {ex.Message}";
        }
    }

    public void UndoLastCurve()
    {
        bool success = _graphDataService.UndoLastStroke(LiveDataPoints);
        if (success)
        {
            SystemStatus = "↩️ Son çizilen eğri başarıyla geri alındı.";
        } else
        {
            SystemStatus = "ℹ️ Geri alınacak geçici çizim geçmişi bulunmuyor.";
        }
    }

    public void ClearStreamData()
    {
        LiveDataPoints.Clear();
        _graphDataService.ClearHistory();
        _currentOrderIndex = 1;
        SystemStatus = "🧹 Ekran ve geçici hafıza temizlendi.";
    }
}
