namespace TEIGraphDataExtractor.Services;

public interface ICoordinateConverter
{
    bool IsCalibrated {get; }
    void Calibrate(double minPixX, double maxPixX, double minPixY, double maxPixY,
        double realMinX, double realMaxX, double realMinY, double realMaxY);
    (double RealX, double RealY) PixelToRealWorld(double pixelX, double pixelY);
    (double PixelX, double PixelY) RealWorldToPixel(double realX, double realY);
}