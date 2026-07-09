using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TEIGraphDataExtractor.Models;

namespace TEIGraphDataExtractor.Services.Export
{
    public class CsvExportStrategy : IExportStrategy
    {
        public bool Export(IEnumerable<DataPoint> dataPoints, string filePath)
        {
            if (dataPoints == null || !dataPoints.Any()) 
                throw new InvalidOperationException("Dışa aktarılacak hiçbir veri noktası bulunamadı!");
            
            try
            {
                var sb = new StringBuilder();

                sb.AppendLine("Index,X_Value,Y_Value,Z_Group");

                foreach (var point in dataPoints)
                {
                    string xStr = point.XValue.ToString("F4", CultureInfo.InvariantCulture);
                    string yStr = point.YValue.ToString("F4", CultureInfo.InvariantCulture);
                    string zStr = point.ZValue.ToString("F2", CultureInfo.InvariantCulture);

                    sb.AppendLine($"{point.OrderIndex},{xStr},{yStr},{zStr}");
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return true;
            } catch (Exception ex)
            {
                Console.WriteLine($"[❌ CSV DIŞA AKTARMA HATASI]: {ex.Message}");
                return false;
            }
        }
    }
}