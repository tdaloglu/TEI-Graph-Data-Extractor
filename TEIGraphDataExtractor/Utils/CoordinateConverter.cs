using System;

namespace TEIGraphDataExtractor.Services
{
    /// <summary>
    /// Piksel koordinatlarını gerçek dünya kontur grafiği değerlerine dönüştüren servis.
    /// Single Responsibility (SRP): Yalnızca kalibrasyon oranları ve koordinat matematiğinden sorumludur.
    /// </summary>
    public class CalibrationService
    {
        // Gerçek dünya sınır değerleri
        public double RealXMin { get; private set; }
        public double RealXMax { get; private set; }
        public double RealYMin { get; private set; }
        public double RealYMax { get; private set; }

        // Ekranda tıklanan referans piksel koordinatları
        public double OriginPixelX { get; private set; }
        public double OriginPixelY { get; private set; }
        public double XMaxPixelX { get; private set; }
        public double YMaxPixelY { get; private set; }

        // Kalibrasyonun başarıyla yapılıp yapılmadığını takip eden bayrak
        public bool IsCalibrated { get; private set; } = false;

        // Dönüşüm katsayıları (Önceden hesaplanarak Stream Mode performansı artırılır)
        private double _scaleX;
        private double _scaleY;

        /// <summary>
        /// Kalibrasyon parametrelerini ayarlar ve dönüşüm katsayılarını hesaplar.
        /// </summary>
        public void Calibrate(
            double originPxX, double originPxY,
            double xMaxPxX, double yMaxPxY,
            double realXMin, double realXMax,
            double realYMin, double realYMax)
        {
            // 1. GÜVENLİK KONTROLÜ: Sıfıra Bölünme (Division by Zero) Engellemesi
            // Eğer kullanıcı aynı piksele tıkladıysa veya fark çok küçükse (< 0.0001) hata fırlat
            double pixelDeltaX = Math.Abs(xMaxPxX - originPxX);
            double pixelDeltaY = Math.Abs(originPxY - yMaxPxY);

            if (pixelDeltaX < 0.0001 || pixelDeltaY < 0.0001)
            {
                IsCalibrated = false;
                throw new InvalidOperationException("Kalibrasyon noktaları birbirinden çok yakın veya aynı! Sıfıra bölünme hatası (Division by Zero) engellendi. Lütfen noktaları doğru seçin.");
            }

            if (Math.Abs(realXMax - realXMin) < 0.0001 || Math.Abs(realYMax - realYMin) < 0.0001)
            {
                IsCalibrated = false;
                throw new ArgumentException("Gerçek eksen minimum ve maksimum değerleri aynı olamaz!");
            }

            // Değerleri ata
            OriginPixelX = originPxX;
            OriginPixelY = originPxY;
            XMaxPixelX = xMaxPxX;
            YMaxPixelY = yMaxPxY;

            RealXMin = realXMin;
            RealXMax = realXMax;
            RealYMin = realYMin;
            RealYMax = realYMax;

            // 2. KATSAYI HESAPLAMA (Önceden hesaplayarak fare hareketinde işlem yükünü azaltıyoruz)
            _scaleX = (RealXMax - RealXMin) / (XMaxPixelX - OriginPixelX);
            
            // Ekran Y koordinatları aşağı doğru arttığı için (OriginPixelY > YMaxPixelY) olur.
            _scaleY = (RealYMax - RealYMin) / (OriginPixelY - YMaxPixelY);

            IsCalibrated = true;
        }

        /// <summary>
        /// Ekrandan gelen piksel (X, Y) noktasını gerçek dünya değerlerine dönüştürür.
        /// </summary>
        public (double RealX, double RealY) PixelToRealWorld(double pixelX, double pixelY)
        {
            if (!IsCalibrated)
            {
                throw new InvalidOperationException("Kalibrasyon yapılmadan koordinat dönüşümü gerçekleştirilemez!");
            }

            // X Dönüşümü: Orijinden ne kadar sağa gidildiği * ölçek
            double realX = RealXMin + (pixelX - OriginPixelX) * _scaleX;

            // Y Dönüşümü: Orijinden ne kadar yukarı gidildiği (Ekran tersliği sebebiyle OriginY - pixelY) * ölçek
            double realY = RealYMin + (OriginPixelY - pixelY) * _scaleY;

            // Math.Round ile virgüllü sayı hatalarını (Floating point precision) sınırlayabiliriz (Örn: 4 basamak)
            return (Math.Round(realX, 4), Math.Round(realY, 4));
        }
    }
}