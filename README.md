# Kolay Yazdır

Kırtasiyede günlük çıktı işini tek pencerede bitiren Windows uygulaması.
Windows'un yazdırma ayarlarındaki dağınıklığı gizler, sadece gerçekten
kullanılan seçenekleri bırakır.

## Ne yapar

- Görsel, PDF, Word ve Excel dosyalarını birlikte yazdırır
- A4 / A5 / A3, dikey / yatay, renkli / siyah beyaz
- Önlü arkalı — çevirme kenarını yönden kendisi seçer (dikeyse uzun kenar,
  yataysa kısa kenar)
- Kağıt cinsini yazıcının kendi listesinden seçtirir ("Düz", "Kalın 1")
- Bir sayfaya 1 / 2 / 4 / 9 / 16 / 35 sayfa yerleştirir
- Sayfaya sığdır seçeneği (varsayılan **kapalı**: içerik gerçek boyutunda
  basılır, sadece taşarsa küçültülür — vesikalık gibi ölçüsü önemli işler
  bozulmaz)
- Sayfa aralığı ve kopya sayısı
- Canlı önizleme — ekranda gördüğün, kağıda çıkanın ta kendisi

## Kurulum

[Sürümler](../../releases) sayfasından son `KolayYazdir-win-Setup.exe`
dosyasını indir ve çalıştır. Yönetici şifresi istemez.

Kurulumdan sonra uygulama kendini günceller; bir daha indirmen gerekmez.

## Gereksinimler

- Windows 10 veya üstü
- Word ve Excel yazdırmak için LibreOffice veya Microsoft Office
  (ikisi de varsa LibreOffice tercih edilir: başsız kipte her makinede aynı
  davranır, Word otomasyonu sürüme göre kip penceresi açıp dönüşümü düşürebilir)

## Nasıl çalışıyor

Yerleşim hesabı (`LayoutEngine`) hiçbir çizim yapmayan saf bir fonksiyondur:
sayfa boyutlarını ve ayarları alır, "hangi sayfa hangi dikdörtgene, kaç derece
dönük" bilgisini veren bir yaprak listesi üretir. Aynı liste hem önizlemeye
hem yazıcıya `SheetRenderer` ile çizilir — önizlemenin çıktıdan sapması bu
yüzden yapısal olarak imkansızdır.

Word ve Excel dosyaları önce PDF'e çevrilir, böylece PDF ve görsel dışında
ayrı bir kod yolu kalmaz. Dönüşüm sırayla LibreOffice ve Microsoft Office'i
dener; ikisinin de süre sınırı vardır ve başarısızlık sebebi olduğu gibi
ekrana taşınır.

```
src/KolayYazdir.Core        saf yerleşim matematiği (Windows'a bağlı değil)
src/KolayYazdir.Documents   dosya okuma, PDF/görsel/Office
src/KolayYazdir.Printing    çizim, yazıcı sürücüsü, DEVMODE
src/KolayYazdir.App         WPF arayüzü
```

## Geliştirme

```bash
dotnet test
dotnet run --project src/KolayYazdir.App
```

Tasarım kararları `docs/superpowers/specs/`, uygulama planı
`docs/superpowers/plans/` altında.

## Yerinde doğrulanması gerekenler

Aşağıdakiler yazıcıdan yazıcıya değişir ve dükkandaki gerçek yazıcıda
sınanmalıdır:

1. Elle önlü arkalı sayfa sırası — kağıdı yüzü aşağı çıkaran yazıcılara göre
   yazıldı; yüzü yukarı çıkaran bir yazıcıda sıra ters olur.
2. Kağıt cinsi isimlerinin sürücüden geldiği ("Düz", "Kalın 1"). Sürücü liste
   vermezse "Düz / Kalın" yedek eşlemesi devreye girer.
3. Kenar boşluklarının gerçek çıktıda beklendiği gibi olduğu.
