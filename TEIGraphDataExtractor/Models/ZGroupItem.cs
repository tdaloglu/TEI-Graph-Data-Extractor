using System.ComponentModel;
using Avalonia.Data;

namespace TEIGraphDataExtractor.Models
{
    public partial class ZGroupItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
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
                _zValue = value; 
                _zValueStr = value.ToString(); // Koddan atama yapılırsa (örn: ZValue = 0.0), arayüzdeki metni de eşitle!
                
                OnPropertyChanged(nameof(ZValue)); 
                OnPropertyChanged(nameof(ZValueInput)); 
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }
    }
}