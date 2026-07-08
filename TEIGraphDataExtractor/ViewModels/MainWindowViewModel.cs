using System;
using Avalonia.Media.Imaging;
using TEIGraphDataExtractor.Services;
using TEIGraphDataExtractor.Utils;


namespace TEIGraphDataExtractor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
   public CoordinateConverter Converter { get; } = new CoordinateConverter();
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
        set { _graphImage = value; RaisePropertyChanged(); }
    }
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



}
