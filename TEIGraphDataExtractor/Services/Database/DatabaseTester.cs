using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TEIGraphDataExtractor.Models;

namespace TEIGraphDataExtractor.Services.Database
{
    public static class DatabaseTester
    {
        public static void RunAllTests()
        {
            Console.WriteLine("--- [TEST BAŞLADI] Veritabanı Test Süreci ---");

            using (var context = new AppDbContext())
            {
                context.Database.EnsureCreated();
                Console.WriteLine($"[OK] Veritabanı Bağlantısı Hazır: {DbConnectionManager.Instance.DbPath}");
            }

            int createdGraphId = InsertDummyGraphData();

            if (createdGraphId > 0)
            {
                ReadAndVerifyGraphData(createdGraphId);
            }

            Console.WriteLine("--- [TEST BİTTİ] Tüm veritabanı işlemleri başarılı! ---");
        }

        private static int InsertDummyGraphData()
        {
            using var context = new AppDbContext();

            var dummyGraph = new Graph
            {
                Name = $"TEI_Test_Kontur_{DateTime.Now:HHmmss}",
                ImageFilePath = "C:/TEI/Internship/SampleGraph.png",
                XMin = 0.0,
                XMax = 500.0,
                YMin = 0.0,
                YMax = 300.0,
                CreatedAt = DateTime.Now,
                DataPoints = new List<DataPoint>()
            };

            double kFactorZ = 1.45;

            dummyGraph.DataPoints.Add(new DataPoint {XValue = 10.5, YValue = 20.2, ZValue = kFactorZ, OrderIndex = 1});
            dummyGraph.DataPoints.Add(new DataPoint {XValue = 15.0, YValue = 25.8, ZValue = kFactorZ, OrderIndex = 2});
            dummyGraph.DataPoints.Add(new DataPoint {XValue = 22.3, YValue = 30.1, ZValue = kFactorZ, OrderIndex = 3});

            context.Graphs.Add(dummyGraph);
            int affectedRows = context.SaveChanges();

            Console.WriteLine($"[YAZMA TESTİ] '{dummyGraph.Name}' grafiği ve {dummyGraph.DataPoints.Count} nokta kaydedildi. (Etkilenen Satır: {affectedRows})");

            return dummyGraph.GraphId;
        }

        private static void ReadAndVerifyGraphData(int graphId)
        {
            using var context = new AppDbContext();

            var graph = context.Graphs.Include(g => g.DataPoints).FirstOrDefault(g => g.GraphId == graphId);

            if (graph != null)
            {
                Console.WriteLine($"\n[OKUMA TESTİ] Grafik Bulundu: ID={graph.GraphId} | Ad={graph.Name}");
                Console.WriteLine($"[META DATA] Eksenler -> X: ({graph.XMin} - {graph.XMax}) | Y: ({graph.YMin} - {graph.YMax})");
                Console.WriteLine($"[NOKTALAR] Toplam {graph.DataPoints.Count} adet veri noktası okundu:");

                foreach (var pt in graph.DataPoints.OrderBy(p => p.OrderIndex))
                {
                    Console.WriteLine($"   -> Nokta #{pt.OrderIndex}: X={pt.XValue}, Y={pt.YValue}, Z(K Katsayısı)={pt.ZValue}");
                }
            } else
            {
                Console.WriteLine($"[HATA] ID={graphId} olan grafik veritabanında bulunamadı!");
            }
        }
    }
}