using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TEIGraphDataExtractor.Models
{
    [Table("DataPoints")]
    public class DataPoint : INotifyPropertyChanged
    {
        [Key]
        public int DataPointId { get; set; }

        [Required]
        public int GraphId { get; set; }

        private double _xValue;
        public double XValue
        {
            get => _xValue;
            set { _xValue = value; OnPropertyChanged(); }
        }

        private double _yValue;
        public double YValue
        {
            get => _yValue;
            set { _yValue = value; OnPropertyChanged(); }
        }

        private double _zValue;
        public double ZValue
        {
            get => _zValue;
            set { _zValue = value; OnPropertyChanged(); }
        }

        public int ZGroupId {get; set; } = 1;

        public int OrderIndex { get; set; }

        [ForeignKey(nameof(GraphId))]
        public virtual Graph Graph { get; set; } = null!;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}