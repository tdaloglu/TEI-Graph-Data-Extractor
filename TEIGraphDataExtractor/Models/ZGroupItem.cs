using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TEIGraphDataExtractor.Models
{
    public partial class ZGroupItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public int Id {get; set; }
        public string ColorHex {get; set; } = "#FF4D4D";

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
            set {_isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        public Action<string>? StatusReporter {get; set; }

        public Action<int, double>? OnValueChanged {get; set; }

        private string _zValueText = "0,000";
        public string ZValueText
        {
            get => _zValueText;
            set
            {
                _zValueText = value;
                RaisePropertyChanged();

                if (string.IsNullOrWhiteSpace(value)) return;

                string sanitized = value.Replace('.', ',');
                if (double.TryParse(sanitized, out double res))
                {
                    _zValue = res;
                    RaisePropertyChanged(nameof(ZValue));
                    StatusReporter?.Invoke("✅ Z değeri başarıyla güncellendi.");

                    OnValueChanged?.Invoke(Id, _zValue);
                } else
                {
                    StatusReporter?.Invoke($"⚠️ HATA: 'Grup {Id} Z Değeri' alanına sadece sayı girebilirsiniz!");
                }
            }
        }
    }
}