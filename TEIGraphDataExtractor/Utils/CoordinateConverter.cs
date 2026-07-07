namespace TEIGraphDataExtractor.Utils;

public class CoordinateConverter{
    
    public double PixelX1 { get; set; }
    public double PixelX2 { get; set; }
    public double PixelY1 { get; set; }
    public double PixelY2 { get; set; }


    public double RealX1 { get; set; }
    public double RealX2 { get; set; }
    public double RealY1 { get; set; }
    public double RealY2 { get; set; }

    public bool IsCalibrated => PixelX1 != PixelX2 && PixelY1 != PixelY2 && RealX1 != RealX2 && RealY1 != RealY2;
    


    public (double X, double Y) PixelToReal(double currentPixelX, double currentPixelY){
        if (!IsCalibrated){
            throw new System.InvalidOperationException("Lütfen Önce 4 noktayı seçerek kalibrasyonu tamamlayınız.");
        }

        double realX = RealX1 + (currentPixelX - PixelX1) * ((RealX2 - RealX1) / (PixelX2 - PixelX1));
        double realY = RealY1 + (currentPixelY - PixelY1) * ((RealY2 - RealY1) / (PixelY2 - PixelY1));

        return (realX, realY);
    }
}