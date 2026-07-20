using System;
using Avalonia.Media;

namespace TEIGraphDataExtractor.Models;

public partial class ZGroupItem
{
    public int ID { get; set; }
    public IBrush GroupColor { get; set; } = Brushes.Red;
}
