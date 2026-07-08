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

        public double InputRealX1 {get; set; } = 0.0;
        public double InputRealX2 {get; set; } = 1.0;
        public double InputRealY1 {get; set; } = 0.0;
        public double InputRealY2 {get; set; } = 1.0;

        private (double X, double Y)? _x1Pixel;
        private (double X, double Y)? _x2Pixel;
        private (double X, double Y)? _y1Pixel;
        private (double X, double Y)? _y2Pixel;

        private CalibrationStep _currentStep = CalibrationStep.WaitingForX1;
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
            CalibrationStep.WaitingForX1 => "📍 Lütfen X1 referans noktasına tıklayın (Örn: X ekseninin başlangıcı).",
            CalibrationStep.WaitingForX2 => "➡️ Lütfen X2 referans noktasına tıklayın (Örn: X ekseninin bitişi).",
            CalibrationStep.WaitingForY1 => "📍 Lütfen Y1 referans noktasına tıklayın (Örn: Y ekseninin başlangıcı/altı).",
            CalibrationStep.WaitingForY2 => "⬆️ Lütfen Y2 referans noktasına tıklayın (Örn: Y ekseninin bitişi/üstü).",
            CalibrationStep.Completed => "✅ Kalibrasyon Başarılı! Artık 'Kalem (Seri Çizim)' ile tarama yapabilirsiniz.",
            _ => "Hazır."
        };

        public CalibrationViewModel(CoordinateConverter coordinateConverter)
        {
            _coordinateConverter = coordinateConverter;
        }

        public void SelectCalibrationPoint(CalibrationStep step)
        {
            CurrentStep = step;
        }

        public void ProcessPixelClick(double pixelX, double pixelY)
        {
            switch (CurrentStep)
            {
                case CalibrationStep.WaitingForX1:
                    _x1Pixel = (pixelX, pixelY);
                    CurrentStep = CalibrationStep.WaitingForX2;
                    break;
                
                case CalibrationStep.WaitingForX2:
                    _x2Pixel = (pixelX, pixelY);
                    CurrentStep = CalibrationStep.WaitingForY1;
                    break;
                
                case CalibrationStep.WaitingForY1:
                    _y1Pixel = (pixelX, pixelY);
                    CurrentStep = CalibrationStep.WaitingForY2;
                    break;
                
                case CalibrationStep.WaitingForY2:
                    _y2Pixel = (pixelX, pixelY);
                    break;
                
                case CalibrationStep.Completed:
                    break;
            }

            if (_x1Pixel.HasValue && _x2Pixel.HasValue && _y1Pixel.HasValue && _y2Pixel.HasValue)
            {
                ExecuteCalibration();
            }
        }

        private void ExecuteCalibration()
        {
            try
            {
                _coordinateConverter.Calibrate(
                    x1PxX: _x1Pixel!.Value.X, x2PxX: _x2Pixel!.Value.X,
                    y1PxY: _y1Pixel!.Value.Y, y2PxY: _y2Pixel!.Value.Y,
                    realX1: InputRealX1, realX2: InputRealX2,
                    realY1: InputRealY1, realY2: InputRealY2
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
            _x1Pixel = null;
            _x2Pixel = null;
            _y1Pixel = null;
            _y2Pixel = null;
            CurrentStep = CalibrationStep.WaitingForX1;
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