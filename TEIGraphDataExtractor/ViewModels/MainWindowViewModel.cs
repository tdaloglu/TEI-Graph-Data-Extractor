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
using System.Text.RegularExpressions;


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
    private string _groupCountText = "0";
    public string GroupCountText
    {
        get => _groupCountText;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _))
            {
                throw new Avalonia.Data.DataValidationException("Lütfen geçerli bir tam sayı giriniz.");
            }   
            _groupCountText = value;
            RaisePropertyChanged();

            if (int.TryParse(value, out int parsedValue))
            {
                if (parsedValue > 15)
                {
                    parsedValue = 15;
                    _groupCountText = "15";
                    RaisePropertyChanged();
                } else if (parsedValue < 0)
                {
                    parsedValue = 0;
                    _groupCountText = "0";
                    RaisePropertyChanged();
                }

                _groupCount = parsedValue;
                GenerateZGroups();
            } else if (string.IsNullOrWhiteSpace(value))
            {
                _groupCount = 0;
                GenerateZGroups();
            }
        }
    }


   // --- ARAYÜZ (STRING) DEĞİŞKENLERİ ---
    private string _realXMinStr = "0";
    public string RealXMin
    {
        get => _realXMinStr;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !double.TryParse(value, out double result))
                throw new Avalonia.Data.DataValidationException("Lütfen geçerli bir sayı girin.");
                
            _realXMinStr = value;
            RealXMinDouble = result;
            RaisePropertyChanged();
        }
    }

    private string _realXMaxStr = "0";
    public string RealXMax
    {
        get => _realXMaxStr;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !double.TryParse(value, out double result))
                throw new Avalonia.Data.DataValidationException("Lütfen geçerli bir sayı girin.");
                
            _realXMaxStr = value;
            RealXMaxDouble = result;
            RaisePropertyChanged();
        }
    }

    private string _realYMinStr = "0";
    public string RealYMin
    {
        get => _realYMinStr;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !double.TryParse(value, out double result))
                throw new Avalonia.Data.DataValidationException("Lütfen geçerli bir sayı girin.");
                
            _realYMinStr = value;
            RealYMinDouble = result;
            RaisePropertyChanged();
        }
    }

    private string _realYMaxStr = "0";
    public string RealYMax
    {
        get => _realYMaxStr;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !double.TryParse(value, out double result))
                throw new Avalonia.Data.DataValidationException("Lütfen geçerli bir sayı girin.");
                
            _realYMaxStr = value;
            RealYMaxDouble = result;
            RaisePropertyChanged();
        }
    }

    // --- ARKA PLANDAKİ MATEMATİK İÇİN (DOUBLE) DEĞİŞKENLER ---
    public double RealXMinDouble { get; set; } = 0.0;
    public double RealXMaxDouble { get; set; } = 0.0;
    public double RealYMinDouble { get; set; } = 0.0;
    public double RealYMaxDouble { get; set; } = 0.0;

    // --- PİKSEL DEĞERLERİNİ TUTACAK GEÇİCİ DEĞİŞKENLER ---
    public double MinPixelX { get; set; }
    public double MinPixelY { get; set; }
    public double XMaxPixelX { get; set; }
    public double YMaxPixelY { get; set; }

    //artık tüm noktalar secildiginde asagıdaki satırlar calısır

    private double _activeZValue = 0;
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

private bool _showCalibrationWarning = false;
public bool ShowCalibrationWarning
    {
        get => _showCalibrationWarning;
        set { _showCalibrationWarning=value; RaisePropertyChanged(); }
    }
public async void TriggerCalibrationWarning()
    {
        ShowCalibrationWarning = true;
        try
        {
            await System.Threading.Tasks.Task.Delay(10000); // 10sn
            ShowCalibrationWarning = false; // süre sonunda kpaat
        }
        catch { }
    }
public void CloseCalibrationWarning()
    {
        ShowCalibrationWarning = false;
    }

public bool TryCalibrate()
    {
        try
        {
            // BURASI DEĞİŞTİ: Artık sonu 'Double' ile biten gerçek sayısal değişkenlerimizi gönderiyoruz
            Converter.Calibrate(MinPixelX, XMaxPixelX, MinPixelY, YMaxPixelY, RealXMinDouble, RealXMaxDouble, RealYMinDouble, RealYMaxDouble);
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
        if (!Converter.IsCalibrated && _groupCount == 0)
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
// --- RESİM YOK UYARISI KONTROLCÜLERİ ---
    private bool _showNoImageWarning = false;
    public bool ShowNoImageWarning
    {
        get => _showNoImageWarning;
        set { _showNoImageWarning = value; RaisePropertyChanged(); }
    }

    public async void TriggerNoImageWarning()
    {
        ShowNoImageWarning = true;
        try
        {
            await System.Threading.Tasks.Task.Delay(10000); // 10 saniye bekle
            ShowNoImageWarning = false; 
        }
        catch { } 
    }

    public void CloseNoImageWarning()
    {
        ShowNoImageWarning = false;
    }
private bool _showGroupWarning = false;
public bool ShowGroupWarning
    {
        get => _showGroupWarning;
        set { _showGroupWarning=value; RaisePropertyChanged(); }
    }
public bool HasSelectedGroup
    {
        get
        {
            foreach (var group in ZGroups)
            {
                if (group.IsActive) return true;
            }
            return false;
        }
    }
public async void TriggerGroupWarning()
    {
        ShowGroupWarning = true;
        try
        {
            await System.Threading.Tasks.Task.Delay(10000); // 10 saniye (10000 ms) bekle
            ShowGroupWarning = false; // Süre dolunca otomatik kapat
        }
        catch { } // Görev iptal edilirse çökmemesi için
    }

    public void CloseWarning()
    {
        ShowGroupWarning = false;
    }
// --- HİÇ GRUP YOK UYARISI KONTROLCÜLERİ ---
    private bool _showNoGroupWarning = false;
    public bool ShowNoGroupWarning
    {
        get => _showNoGroupWarning;
        set { _showNoGroupWarning = value; RaisePropertyChanged(); }
    }

    // Uyarıyı açıp 10 saniye sonra kapatan Asenkron Motor
    public async void TriggerNoGroupWarning()
    {
        ShowNoGroupWarning = true;
        try
        {
        }
        catch { } 
    }

    public void CloseNoGroupWarning()
    {
        ShowNoGroupWarning = false;
    }


    public ObservableCollection<ZGroupItem> ZGroups {get; } = new();

    public void GenerateZGroups()
    {
        string[] colors = {
    "#FF3B30", // Canlı Kırmızı
    "#007AFF", // Dijital Mavi
    "#34C759", // Zümrüt Yeşili
    "#FF9500", // Enerjik Turuncu
    "#AF52DE", // Neon Mor
    "#FFCC00", // Parlak Sarı
    "#5AC8FA", // Gökyüzü Mavisi
    "#FF2D55", // Pembe / Fuşya
    "#00E1E2", // Turkuaz
    "#E0A96D", // Altın / Şampanya
    "#81C784", // Açık Yeşil
    "#ca66b9",
    "#3bae9f",
    "#311a9b"
    };

        while (ZGroups.Count < _groupCount)
        {
            ZGroups.Add(new ZGroupItem
            {
                Id = ZGroups.Count + 1,
                ZValue = 0.0,
                ColorHex = colors[ZGroups.Count % colors.Length],
                IsActive = false
            });
        }

        while (ZGroups.Count > _groupCount)
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

    public void RemoveZGroup(ZGroupItem groupToRemove)
    {
        if (groupToRemove == null) return;

        if (ZGroups.Contains(groupToRemove))
        {
            bool wasActive = groupToRemove.IsActive;
            ZGroups.Remove(groupToRemove);

            _groupCount = ZGroups.Count;
            GroupCountText = _groupCount.ToString();

            if (wasActive && ZGroups.Count > 0)
            {
                SetActiveGroup(ZGroups[0]);
            } else if (ZGroups.Count == 0)
            {
                ActiveZValue = 0.0;
                SystemStatus = "⚠️ Hiçbir Z grubu kalmadı. Lütfen yeni bir grup ekleyin.";
            }

            for (int i = 0; i < ZGroups.Count; i++)
            {
                ZGroups[i].Id = i + 1;
            }
        }
    }
}
