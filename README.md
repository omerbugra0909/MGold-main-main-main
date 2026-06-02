# MGold Kuyumculuk Yonetim Sistemi

MGold; ASP.NET Core, EF Core, Razor Views, JWT/Cookie authentication ve SignalR kullanan kuyumculuk yonetim projesidir. Proje; musteri, calisan, firma yoneticisi ve sistem admini rollerini ayirarak urun, stok, siparis, odeme, fatura, bildirim, canli piyasa, dashboard ve raporlama akislari sunar.

## Sinav Kriterleri Karsiligi

- Login/Register: Musteri kaydi, musteri girisi, admin/workspace girisi vardir.
- Mail dogrulama: Kayit sonrasi e-posta dogrulama akisi ve tekrar gonderme ekrani vardir.
- Sifre sifirlama: E-posta/SMS kanali secimli sifre yenileme akisi vardir.
- Roller: `SystemAdmin`, `Manager`, `Employee`, `Customer`.
- CRUD: Urun, musteri, islem, siparis, kullanici, gorev, yorum ve bildirim akislari bulunur.
- Dashboard: Admin/firma panelinde grafik destekli isletme ozeti vardir.
- Raporlama: Kar/zarar raporu tarih filtresiyle listelenir ve `Yazdir / PDF` butonu ile PDF'e yazdirilabilir.
- PDF cikti: Siparis faturalari PDF olarak uretilir ve indirilebilir.
- API: Urun, musteri, islem, siparis, rapor, market, auth ve bildirim endpointleri vardir.
- Responsive UI: Public, admin, customer ve workforce layoutlari mobil uyumludur.
- Firma izolasyonu: Manager ve firma kullanicilari kendi firma verileriyle sinirlandirilir.

## Paneller

- `/auth`: Giris merkezi
- `/auth/register`: Musteri kaydi
- `/auth/forgot-password`: Sifre yenileme
- `/admin/login`: Sistem admini girisi
- `/workspace/login`: Firma yoneticisi/calisan girisi
- `/admin`: Sistem admin dashboard
- `/owner`: Firma yoneticisi dashboard
- `/employee`: Calisan gorev alani
- `/customer`: Musteri paneli
- `/owner/reports` veya `/admin/reports`: Filtreli rapor ekrani

## Demo Roller

Development modunda seed mekanizmasi demo firma, urun, musteri, siparis, kullanici ve gorev verilerini olusturur. Demo hesap bilgileri giris ekranlarinda gosterilir.

## Calistirma

```bash
dotnet restore
dotnet build MGold.sln
dotnet run
```

Development ayarlari varsayilan olarak SQLite kullanir:

- `App:UseSqlite=true`
- `App:AutoMigrate=true`
- `App:SeedDemoData=true`

## Teknik Yapi

- `Domain`: Entity ve enum modelleri
- `Application`: DTO, servis, is kurallari, fiyatlama ve raporlama
- `Infrastructure`: EF Core DbContext ve repository katmani
- `Controllers`: API ve Razor panel controllerlari
- `Views`: Public, admin, customer, workforce ve account ekranlari
- `Middleware`: Global hata yakalama ve audit log
- `Hubs`: SignalR market bildirimleri

## Dogrulama

Son kontrolde:

- `dotnet build MGold.sln` basarili.
- `/api/health` endpointi 200 OK donuyor.
- `/auth/forgot-password` sayfasi 200 OK donuyor.
