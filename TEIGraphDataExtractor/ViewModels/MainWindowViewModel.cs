using System;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using TEIGraphDataExtractor.Models;
using TEIGraphDataExtractor.Services;
using TEIGraphDataExtractor.Utils;
using TEIGraphDataExtractor.Services.Database;
using TEIGraphDataExtractor.Services.Export;
using Avalonia.Diagnostics;
using System.IO;


namespace TEIGraphDataExtractor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
   public CoordinateConverter Converter { get; } = new CoordinateConverter();
   private readonly GraphDataService _graphDataService = new GraphDataService();
   private string _systemStatus = "✅ Sistem Hazır. Lütfen görsel yükleyiniz.";
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
    private int _groupCount = 0;
    public int GroupCount
    {
        get => _groupCount;
        set
        {
            if (_groupCount != value && value > 0 && value <= 15)
            {
                _groupCount = value;
                RaisePropertyChanged();
                GenerateZGroups();
            } 
        }
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

    private bool _isDeleteModeActive = false;
    public bool IsDeleteModeActive
    {
        get => _isDeleteModeActive;
        set
        {
            _isDeleteModeActive = value;
            RaisePropertyChanged();

            SystemStatus = value 
                ? "🎯 SİLME MODU AKTİF: Silmek istediğiniz noktanın üzerine tıklayın." 
                : "✏️ ÇİZİM MODU: Farenizle tarama yapabilirsiniz.";
        }
    }

    public void ToggleDeleteMode()
    {
        IsDeleteModeActive = !IsDeleteModeActive;
    }

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

    public void DeletePoint(DataPoint? pointToDelete)
    {
        if (pointToDelete == null)
        {
            Console.WriteLine("[UYARI] Silinmek istenen nokta boş (NULL) geldi!");
            return;
        }

        if (LiveDataPoints.Contains(pointToDelete))
        {
            LiveDataPoints.Remove(pointToDelete);
            Console.WriteLine($"[NOKTA SİLİNDİ - RAM TEMİZLENDİ] 🗑️ RAM'de Kalan Nokta Sayısı: {LiveDataPoints.Count}");
        } else
        {
            Console.WriteLine($"[UYARI] Nokta #{pointToDelete.OrderIndex} zaten RAM listesinde bulunamadı!");
        }

        if (pointToDelete.DataPointId > 0)
        {
            try
            {
                using var context = new AppDbContext();
                context.DataPoints.Remove(pointToDelete);
                context.SaveChanges();

                SystemStatus = $"🗑️ Nokta #{pointToDelete.OrderIndex} veritabanından ve ekrandan silindi.";
                Console.WriteLine($"[💾 VERİTABANINDAN SİLİNDİ] ID: {pointToDelete.DataPointId}");
            }
            catch (Exception ex)
            {
                SystemStatus = $"⚠️ Veritabanı Silme Hatası: {ex.Message}";
                Console.WriteLine($"[❌ DB SİLME HATASI]: {ex.Message}");
            }
        } else
        {
            SystemStatus = $"🗑️ Nokta #{pointToDelete.OrderIndex} ekrandan silindi (Henüz DB'ye kaydolmamıştı).";
        }
    }

    public bool TryDeletePointAtPixel(double clickPixelX, double clickPixelY, double hitTolerancePixels = 15.0)
    {
        if (LiveDataPoints.Count == 0) return false;

        DataPoint? closestPoint = null;
        double minDistance = double.MaxValue;

        foreach(var point in LiveDataPoints)
        {
            var ptPixel = Converter.RealWorldToPixel(point.XValue, point.YValue);

            double dx = ptPixel.PixelX - clickPixelX;
            double dy = ptPixel.PixelY - clickPixelY;
            double distance = Math.Sqrt(dx*dx + dy*dy);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestPoint = point;
            }
        }

        if (closestPoint != null && minDistance <= hitTolerancePixels)
        {
            DeletePoint(closestPoint);

            SystemStatus = "ℹ️ Nokta başarı ile silindi.";
            return true;
        } 

        return false;
    }

    public void ExportToCsv(string? customFilePath = null)
    {
        if (LiveDataPoints.Count == 0)
        {
            SystemStatus = "⚠️ Dışa aktarılacak veri yok! Lütfen önce çizim yapın.";
            Console.WriteLine("[UYARI] Dışa aktarılacak nokta bulunamadı.");
            return;
        }

        try
        {
            IExportStrategy exportStrategy = new CsvExportStrategy();

            string targetPath = customFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"TEI_Grafik_Verisi_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            );

            bool success = exportStrategy.Export(LiveDataPoints, targetPath);

            if (success)
            {
                SystemStatus = $"📊 Başarılı: CSV dosyası oluşturuldu -> {Path.GetFileName(targetPath)}";
                Console.WriteLine($"[🔥 BAŞARILI DIŞA AKTARMA] Dosya Yolu: {targetPath}");
            }
        } catch (Exception ex)
        {
            SystemStatus = $"⚠️ CSV İndirme Hatası: {ex.Message}";
            Console.WriteLine($"[❌ EXPORT HATASI]: {ex.Message}");
        }
    }

    public ObservableCollection<ZGroupItem> ZGroups {get; } = new();

    public void GenerateZGroups()
    {
        string[] colors = {"#FF4D4D", "#4DA6FF", "#4DFF4D", "#FFA726", "#FF4DFF", "#FFFF4D"};

        while (ZGroups.Count < GroupCount)
        {
            ZGroups.Add(new ZGroupItem
            {
                Id = ZGroups.Count + 1,
                ZValue = 0.8,
                ColorHex = colors[ZGroups.Count % colors.Length],
                IsActive = ZGroups.Count == 0
            });
        }

        while (ZGroups.Count > GroupCount)
        {
            ZGroups.RemoveAt(ZGroups.Count - 1);
        }
    }

    public void SetActiveGroup(ZGroupItem selectedGroup)
    {
        foreach (var group in ZGroups)
        {
            group.IsActive = (group == selectedGroup);
        }

        ActiveZValue = selectedGroup.ZValue;
        SystemStatus = $"🏷️ Aktif Z Grubu Değişti: Grup {selectedGroup.Id} (Z = {selectedGroup.ZValue})";
    }
}
