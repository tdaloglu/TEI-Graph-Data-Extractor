using System.ComponentModel;

namespace TEIGraphDataExtractor.Models
{
    public partial class ZGroupItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public int Id {get; set; }
        public string ColorHex {get; set; } = "#FF4D4D";

        private double _zValue;
        public double ZValue
        {
            get => _zValue;
            set {_zValue = value; OnPropertyChanged(nameof(ZValue)); }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set {_isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }
    }
}