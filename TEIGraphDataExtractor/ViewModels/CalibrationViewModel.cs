using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TEIGraphDataExtractor.Models;
using TEIGraphDataExtractor.Utils;

namespace TEIGraphDataExtractor.ViewModels
{
    public class CalibrationViewModel : INotifyPropertyChanged
    {
        private readonly CoordinateConverter _coordinateConverter;

        public double InputRealXMin {get; set; } = 0.0;
        public double InputRealXMax {get; set; } = 500.0;
        public double InputRealYMin {get; set; } = 0.0;
        public double InputRealYMax {get; set; } = 300.0;

        private (double X, double Y) _minPixel;
        private (double X, double Y) _xMaxPixel;
        private (double X, double Y) _yMaxPixel;

        private CalibrationStep _currentStep = CalibrationStep.WaitingForXMinYMin;
        public CalibrationStep CurrentStep
        {
            get => _currentStep;
            private set
            {
                _currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public string StatusMessage => CurrentStep switch
        {
            CalibrationStep.WaitingForXMinYMin => "📍 Lütfen eksenlerin başladığı sol alt kesişim noktasına (X-Min, Y-Min) tıklayın.",
            CalibrationStep.WaitingForXMax => "➡️ Lütfen X Ekseninin bittiği en sağ noktaya (X-Max) tıklayın.",
            CalibrationStep.WaitingForYMax => "⬆️ Lütfen Y Ekseninin bittiği en üst noktaya (Y-Max) tıklayın.",
            CalibrationStep.Completed => "✅ Kalibrasyon Başarılı! Artık kontur çizimlerine (Stream Mode) başlayabilirsiniz.",
            _ => "Hazır."
        };

        public CalibrationViewModel(CoordinateConverter coordinateConverter)
        {
            _coordinateConverter = coordinateConverter;
        }

        public void ProcessPixelClick(double pixelX, double pixelY)
        {
            switch (CurrentStep)
            {
                case CalibrationStep.WaitingForXMinYMin:
                    _minPixel = (pixelX, pixelY);
                    CurrentStep = CalibrationStep.WaitingForXMax;
                    break;
                
                case CalibrationStep.WaitingForXMax:
                    _xMaxPixel = (pixelX, pixelY);
                    CurrentStep = CalibrationStep.WaitingForYMax;
                    break;
                
                case CalibrationStep.WaitingForYMax:
                    _yMaxPixel = (pixelX, pixelY);

                    ExecuteCalibration();
                    break;
                
                case CalibrationStep.Completed:
                    break;
            }
        }

        private void ExecuteCalibration()
        {
            try
            {
                _coordinateConverter.Calibrate(
                    minPxX: _minPixel.X, minPxY: _minPixel.Y,
                    xMaxPxX: _xMaxPixel.X, yMaxPxY: _yMaxPixel.Y,
                    realXMin: InputRealXMin, realXMax: InputRealXMax,
                    realYMin: InputRealYMin, realYMax: InputRealYMax
                );

                CurrentStep = CalibrationStep.Completed;
            }
            catch (Exception ex)
            {
                ResetCalibration();
                Console.WriteLine($"[KALİBRASYON HATASI]: {ex.Message}");
            }
        }

        public void ResetCalibration()
        {
            CurrentStep = CalibrationStep.WaitingForXMinYMin;
        }

        public (double RealX, double RealY)? GetRealCoordinates(double pixelX, double pixelY)
        {
            if (!_coordinateConverter.IsCalibrated) return null;
            return _coordinateConverter.PixelToRealWorld(pixelX, pixelY);
        }

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}