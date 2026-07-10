using System;
using Avalonia.Media;

namespace TEIGraphDataExtractor.Models;

public partial class ZGroupItem
{
    public int ID { get; set; }
    public string ZValueText { get; set; } = "Z = 0.0";
    public IBrush GroupColor { get; set; } = Brushes.Red;
}
