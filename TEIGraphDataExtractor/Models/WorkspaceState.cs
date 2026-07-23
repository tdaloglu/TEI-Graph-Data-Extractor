using System.Collections.Generic;
using TEIGraphDataExtractor.Models;

namespace TEIGraphDataExtractor.Models;

public class WorkspaceState
{
    public double MinPixelX {get; set; }
    public double XMaxPixelX {get; set; }
    public double MinPixelY {get; set; }
    public double YMaxPixelY {get; set; }
    public double RealXMinDouble {get; set; }
    public double RealXMaxDouble {get; set; }
    public double RealYMinDouble {get; set; }
    public double RealYMaxDouble {get; set; }
    public string RealXMinStr {get; set; } = "0";
    public string RealXMaxStr {get; set; } = "0";
    public string RealYMinStr {get; set; } = "0";
    public string RealYMaxStr {get; set; } = "0";

    public int GroupCount {get; set; }
    public int CurrentOrderIndex {get; set; } = 1;
    public List<ZGroupItemDto> Groups {get; set; } = new();
    public List<DataPoint> Points {get; set; } = new();
}

public class ZGroupItemDto
{
    public int Id {get; set; }
    public double ZValue {get; set; }
    public string ZValueText {get; set; } = "0";
    public string ColorHex {get; set; } = "#FF0000";
    public bool IsActive {get; set; }
}