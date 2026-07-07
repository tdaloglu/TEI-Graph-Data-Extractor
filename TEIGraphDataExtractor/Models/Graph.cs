using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TEIGraphDataExtractor.Models
{
    [Table("Graphs")]
    public class Graph
    {
        [Key]
        public int GraphId {get; set; }

        [Required]
        [MaxLength(150)]
        public string Name {get; set; } = string.Empty;

        public DateTime CreatedAt {get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? ImageFilePath {get; set; }

        public double XMin {get; set; }
        public double XMax {get; set; }
        public double YMin {get; set; }
        public double YMax {get; set; }

        public virtual ICollection<DataPoint> DataPoints {get; set; } = new List<DataPoint>();
    }
}