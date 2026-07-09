using System;
using System.Dynamic;

namespace TEIGraphDataExtractor.Utils
{
    /// <summary>
    /// Piksel koordinatlarını gerçek dünya kontur grafiği değerlerine dönüştüren servis.
    /// Single Responsibility (SRP): Yalnızca kalibrasyon oranları ve koordinat matematiğinden sorumludur.
    /// </summary>
    public class CoordinateConverter
    {
        public double RealX1 {get; private set; }
        public double RealX2 {get; private set; }
        public double RealY1 {get; private set; }
        public double RealY2 {get; private set; }

        public double X1PixelX {get; private set; }
        public double X2PixelX {get; private set; }
        public double Y1PixelY {get; private set; }
        public double Y2PixelY {get; private set; }

        public bool IsCalibrated {get; private set; } = false;

        private double _scaleX;
        private double _scaleY;

        public void Calibrate(
            double x1PxX, double x2PxX,
            double y1PxY, double y2PxY,
            double realX1, double realX2,
            double realY1, double realY2)
        {
            double pixelDeltaX = Math.Abs(x2PxX - x1PxX);
            double pixelDeltaY = Math.Abs(y1PxY - y2PxY);

            if (pixelDeltaX < 0.0001 || pixelDeltaY < 0.0001)
            {
                IsCalibrated = false;
                throw new InvalidOperationException("Kalibrasyon noktaları birbirinden çok yakın veya aynı! Sıfıra bölünme hatası engellendi.");
            }

            if (Math.Abs(realX2 - realX1) < 0.0001 || Math.Abs(realY2 - realY1) < 0.0001)
            {
                IsCalibrated = false;
                throw new ArgumentException("Gerçek eksen referans değerleri (Örn: X1 ve X2) aynı olamaz!");
            }

            X1PixelX = x1PxX;
            X2PixelX = x2PxX;
            Y1PixelY = y1PxY;
            Y2PixelY = y2PxY;

            RealX1 = realX1;
            RealX2 = realX2;
            RealY1 = realY1;
            RealY2 = realY2;

            _scaleX = (RealX2 - RealX1) / (X2PixelX - X1PixelX);

            _scaleY = (RealY2 - RealY1) / (Y1PixelY - Y2PixelY);

            IsCalibrated = true;
        }

        public (double RealX, double RealY) PixelToRealWorld(double pixelX, double pixelY)
        {
            if (!IsCalibrated)
            {
                throw new InvalidOperationException("Kalibrasyon yapılmadan koordinat dönüşümü gerçekleştirilemez!");
            }

            double realX = RealX1 + (pixelX - X1PixelX) * _scaleX;
            double realY = RealY1 + (Y1PixelY - pixelY) * _scaleY;

            return (Math.Round(realX, 4), Math.Round(realY, 4));
        }

        public (double PixelX, double PixelY) RealWorldToPixel(double realX, double realY)
        {
            if (!IsCalibrated) return (0, 0);

            double pixelX = X1PixelX + (realX - RealX1) / _scaleX;
            double pixelY = Y1PixelY + (realY - RealY1) / _scaleY;

            return (pixelX, pixelY);
        }
    }
}