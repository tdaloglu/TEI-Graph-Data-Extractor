using System.Collections.Generic;
using TEIGraphDataExtractor.Models;

namespace TEIGraphDataExtractor.Services.Export
{
    public interface IExportStrategy
    {
        bool Export(IEnumerable<DataPoint> dataPoints, string filePath);
    }
}