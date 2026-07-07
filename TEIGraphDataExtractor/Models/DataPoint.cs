using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TEIGraphDataExtractor.Models
{
    [Table("DataPoints")]
    public class DataPoint
    {
        [Key]
        public int DataPointId {get; set; }

        [Required]
        public int GraphId {get; set; }

        public double XValue {get; set; }
        public double YValue {get; set; }

        public double ZValue {get; set; }

        public int OrderIndex {get; set; }

        [ForeignKey(nameof(GraphId))]
        public virtual Graph Graph {get; set; } = null!;
    }
}