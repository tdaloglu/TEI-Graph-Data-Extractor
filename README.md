<img width="1342" height="731" alt="image" src="https://github.com/user-attachments/assets/e4de719f-3346-47ea-8424-740f5a55b910" />
# 📈 TEI Graph Data Extractor

![C#](https://img.shields.io/badge/c%23-%23239120.svg?style=for-the-badge&logo=c-sharp&logoColor=white)
![Avalonia UI](https://img.shields.io/badge/Avalonia%20UI-17181C?style=for-the-badge&logo=avalonia&logoColor=white)
![MVVM](https://img.shields.io/badge/Architecture-MVVM-blue?style=for-the-badge)

TEI Graph Data Extractor, mühendislik grafiklerinden ve görsellerden hassas (X, Y, Z) koordinat verilerini çıkarmak için geliştirilmiş, çapraz platform destekli (Cross-platform) bir masaüstü uygulamasıdır. TEI Kariyerini Uçur Yaz Stajı programı kapsamında geliştirilmiştir.

## ✨ Öne Çıkan Özellikler

*   **🎯 Gelişmiş Kalibrasyon Sistemi:** Yüklenen grafik üzerinde (X1, X2, Y1, Y2) piksel noktalarını gerçek dünya değerleriyle eşleştirerek tam isabetli koordinat dönüşümü yapar.
*   **🔍 Akıllı Büyüteç Aracı:** Görselin üzerinde gezinirken piksel hassasiyetinde noktalar seçebilmeniz için canlı büyüteç (magnifier) desteği sunar.
*   **📊 Dinamik Z-Değeri Gruplama:** Verileri 3. boyuta (Z eksenine) göre ayırabilmek için dinamik grup oluşturma, renklendirme ve düzenleme imkanı.
*   **✒️ Çoklu Toplama Modları:**
    *   **Çizim Modu:** Farenizle eğrilerin üzerinden geçerek seri noktalar toplama.
    *   **Tek Nokta Ekleme:** Grafikteki kritik noktalara manuel ve hassas tıklama.
    *   **Taşıma (Adjust) Modu:** Yanlış eklenen noktaları sürükle-bırak ile anında düzeltme.
    *   **Silme Modu:** Ekrandan ve veritabanından hatalı noktaları temizleme.
*   **🛡️ Sağlam Kullanıcı Deneyimi (UX):** Kullanıcının adım atlamasını engelleyen (örneğin resim yüklemeden veya kalibrasyon yapmadan çizim başlatmayı önleyen) akıllı pop-up uyarı sistemi ve %100 tip güvenli veri giriş kutuları.
*   **💾 Dışa Aktarım ve Arşivleme:** Toplanan verileri SQLite/yerel veritabanında saklama ve tek tıkla `.csv` formatında dışa aktarma.

<img width="1336" height="730" alt="image" src="https://github.com/user-attachments/assets/3c671407-66ea-4161-bce5-ba5c9fa6c864" />




## 🛠️ Kullanılan Teknolojiler

*   **Dil:** C#
*   **Arayüz Çerçevesi:** Avalonia UI (XAML)
*   **Mimari:** MVVM (Model-View-ViewModel)
*   **Veritabanı:** Entity Framework Core (AppDbContext)

## 🚀 Kurulum ve Çalıştırma

Projeyi kendi bilgisayarınızda çalıştırmak için aşağıdaki adımları izleyebilirsiniz:

1. Depoyu kendi bilgisayarınıza klonlayın:
   ```bash
   git clone [https://github.com/KULLANICI_ADI/TEI-Graph-Data-Extractor.git](https://github.com/KULLANICI_ADI/TEI-Graph-Data-Extractor.git)
   ```
2. Proje dizinine gidin.
   ```bash
   cd TEI-Graph-Data-Extractor
   ```
3. Gerekli bağımlılıkları yükleyin, projeyi derleyin ve başlatın:
    ```bash
    dotnet restore
    dotnet build
    dotnet run
   ```

   👩‍💻 Geliştiriciler
Başak Heybeli - ODTÜ Bilgisayar Mühendisliği - https://github.com/heybasakk

Tayanç Daloğlu - ODTÜ Bilgisayar Mühendisliği - https://github.com/tdaloglu

Bu proje, 2026 yılı TEI - Kariyerini Uçur Yaz Staj Programı kapsamında ODTÜ stajyer ekibi tarafından geliştirilmiştir.
