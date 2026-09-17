# EgitimHub - Ozel Ders Yonetim Sistemi

EgitimHub, bireysel egitmenlerin ogrenci, ders, odeme, rapor ve finans sureclerini tek panelden yonetmesi icin gelistirilmis ASP.NET Core MVC uygulamasidir.

## Ozellikler

- Ogrenci kayitlari, iletisim bilgileri ve aktiflik takibi
- Tekil ve tekrarli ders planlama
- Ders durumu, sure, konu, not ve ucret yonetimi
- 450-750 TL araliginda yapilandirilmis ders ucreti secimi
- Odeme kayitlari, tarih/ogrenci filtreleri ve toplu tamamlama
- Aylik ders, ogrenci ve gelir raporlari
- PDF ve Excel rapor ciktilari
- Son 12 aylik gelir ve ders trendleri
- Yillik performans karsilastirmasi
- Ogrenci ders sikligi, son ders ve sonraki ders takibi
- Gelir, gider, borc ve yatirim yonetimi
- Kullanici bazli veri izolasyonu
- Aktivite gunlugu, filtreleme, detay ve CSV disari aktarma
- Google OAuth ile mevcut panel hesabina giris
- Google Calendar ve Zoom entegrasyon altyapisi

## Teknolojiler

- .NET 8 / ASP.NET Core MVC
- Entity Framework Core 8
- ASP.NET Core Identity
- Microsoft SQL Server
- Bootstrap 5
- ClosedXML
- QuestPDF
- Google Calendar API
- Google OAuth 2.0
- Zoom Server-to-Server OAuth
- MailKit

## Proje Yapisi

```text
OzelDersProjesi.sln
PrivateTutorTracker/
  Controllers/       MVC controller siniflari
  Data/              EF Core DbContext
  Migrations/        Veritabani migration gecmisi
  Models/            Domain ve view modelleri
  Services/          E-posta, takvim, Zoom ve aktivite servisleri
  Views/             Razor ekranlari
  wwwroot/           CSS, JavaScript ve statik dosyalar
  Program.cs         Uygulama ve middleware yapilandirmasi
```

## Gereksinimler

- .NET 8 SDK
- SQL Server 2019 veya daha yeni bir surum
- Visual Studio 2022, Rider veya VS Code
- EF Core CLI araci

EF aracini kurmak icin:

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

## Yerel Kurulum

1. Repoyu klonlayin:

```powershell
git clone https://github.com/MelihOzaslan01/privateTutorialWebPanelSystem.git
cd privateTutorialWebPanelSystem
```

2. Ornek ayar dosyasini yerel ayar olarak olusturun:

```powershell
Copy-Item .\PrivateTutorTracker\appsettings.example.json .\PrivateTutorTracker\appsettings.Development.json
```

3. `DefaultConnection` degerini kendi SQL Server ortaminiza gore duzenleyin.

4. Paketleri geri yukleyin ve migration'lari uygulayin:

```powershell
dotnet restore .\PrivateTutorTracker\PrivateTutorTracker.sln
dotnet ef database update --project .\PrivateTutorTracker\PrivateTutorTracker.csproj
```

5. Uygulamayi calistirin:

```powershell
dotnet run --project .\PrivateTutorTracker\PrivateTutorTracker.csproj
```

Terminalde gosterilen HTTPS adresini tarayicida acin.

## Guvenli Yapilandirma

Gercek sifreler ve API anahtarlari repoya eklenmemelidir. Gelistirme ortaminda .NET User Secrets kullanin:

```powershell
cd .\PrivateTutorTracker
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
dotnet user-secrets set "EmailSettings:Password" "YOUR_APP_PASSWORD"
dotnet user-secrets set "Zoom:AccountId" "YOUR_ACCOUNT_ID"
dotnet user-secrets set "Zoom:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Zoom:ClientSecret" "YOUR_CLIENT_SECRET"
```

`appsettings*.json`, `web.config`, `service-account-key.json`, publish ciktilari ve veritabani dosyalari `.gitignore` ile dislanmistir.

## Google Girisi

Google Cloud Console'da OAuth istemci turunu **Web application** olarak olusturun. Redirect URI:

```text
https://your-domain.example/signin-google
```

Yerel gelistirme icin kullandiginiz HTTPS portuna ait `/signin-google` adresini de ekleyin.

Gerekli ayarlar:

```text
Authentication:Google:ClientId
Authentication:Google:ClientSecret
Authentication:Google:AllowedEmails:0
Authentication:Google:LinkedUsername
```

Google hesabi otomatik olarak yeni panel kullanicisi olusturmaz. Izinli e-posta, `LinkedUsername` ile belirtilen mevcut hesaba baglanir.

## Veritabani Migration'lari

Yeni migration olusturmak icin:

```powershell
dotnet ef migrations add MigrationName --project .\PrivateTutorTracker\PrivateTutorTracker.csproj
dotnet ef database update --project .\PrivateTutorTracker\PrivateTutorTracker.csproj
```

Canli ortamda migration calistirmadan once hem site hem veritabani yedegi alin. Uygulama, yalnizca acikca etkinlestirildiginde baslangicta migration calistirabilir:

```text
DatabaseInitialization__RunMigrations=true
```

Migration tamamlandiktan sonra bu gecici ayari kaldirin.

## Derleme ve Yayinlama

```powershell
dotnet build .\PrivateTutorTracker\PrivateTutorTracker.sln -c Release
dotnet publish .\PrivateTutorTracker\PrivateTutorTracker.csproj -c Release --no-self-contained -o .\artifacts\publish
```

Canli sunucudaki `appsettings*.json` ve `web.config` dosyalarini yeni publish dosyalariyla kontrolsuz bicimde degistirmeyin. Connection string ve entegrasyon anahtarlari sunucu yapilandirmasinda tutulmalidir.

## Guvenlik

- Uygulama verileri oturum acan kullanicinin kimligine gore filtrelenir.
- Formlarda antiforgery dogrulamasi kullanilir.
- Kimlik dogrulama cookie'si `HttpOnly`, `Secure` ve `SameSite=Lax` olarak yapilandirilmistir.
- Basarisiz girisler icin hesap kilitleme etkindir.
- CSP, HSTS, X-Frame-Options ve nosniff basliklari uygulanir.
- Google girisi izin verilen e-posta listesiyle sinirlandirilir.

Bir anahtar yanlislikla paylasilirsa Git gecmisini temizlemek tek basina yeterli degildir; ilgili servis uzerinden anahtari iptal edip yenisini olusturun.

## Dogrulama

```powershell
dotnet build .\PrivateTutorTracker\PrivateTutorTracker.sln -c Release
```

Canli yayin sonrasinda en az su akislar kontrol edilmelidir:

- Normal ve Google ile giris
- Ogrenci listeleme ve detay
- Ders olusturma ve takvim
- Odeme filtreleme ve toplu tamamlama
- Dashboard metrikleri
- Rapor dashboard'u ve yillik karsilastirma
- PDF/Excel indirme
- Aktivite gunlugu ve CSV cikisi

## Lisans

Bu proje su anda ozel kullanim icin gelistirilmektedir. Acik kaynak lisansi tanimlanmamistir.
