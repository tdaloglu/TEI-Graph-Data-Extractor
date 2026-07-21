using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Data;

namespace TEIGraphDataExtractor.Models
{
    public partial class ZGroupItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public int Id { get; set; }
        public string ColorHex { get; set; } = "#FF4D4D";

        // ==========================================
        // 1. ARAYÜZ (XAML) İÇİN STRING KALKAN
        // ==========================================
        private string _zValueStr = "0";
        public string ZValueInput
        {
            get => _zValueStr;
            set
            {
                // Arayüzden boş veya geçersiz giriş gelirse hata fırlat
                if (string.IsNullOrWhiteSpace(value) || !double.TryParse(value, out double result))
                {
                    throw new DataValidationException("Lütfen geçerli bir sayı giriniz.");
                }

                _zValueStr = value;
                _zValue = result; // Arka plandaki sayıyı da sessizce güncelle!
                
                OnPropertyChanged(nameof(ZValueInput));
                OnPropertyChanged(nameof(ZValue)); // Diğer modüllere sayının değiştiğini haber ver
            }
        }

        // ==========================================
        // 2. DİĞER C# MODÜLLERİ İÇİN DOUBLE DEĞER (Burayı silmiyoruz!)
        // ==========================================
        private double _zValue = 0.0;
        public double ZValue
        {
            get => _zValue;
            set
            {
                if (_zValue != value)
                {
                    _zValue = value;
                    RaisePropertyChanged();

                    _zValueText = value.ToString("F3");
                    RaisePropertyChanged(nameof(ZValueText));

                    OnValueChanged?.Invoke(Id, value);
                }
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public Action<string>? StatusReporter {get; set; }

        public Action<int, double>? OnValueChanged {get; set; }

        private string _zValueText = "0";
        public string ZValueText
        {
            get => _zValueText;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new DataValidationException("Lütfen geçerli bir sayı girin.");
                }

                string sanitizedValue = value.Replace('.', ',');

                if (!double.TryParse(sanitizedValue, out double result))
                {
                    throw new DataValidationException("Lütfen geçerli bir sayı girin.");
                }

                _zValueText = value;
                RaisePropertyChanged();

                ZValue = result;
                OnValueChanged?.Invoke(Id, result);
            }
        }
    }
}