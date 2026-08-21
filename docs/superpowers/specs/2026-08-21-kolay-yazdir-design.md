# Kolay Yazdır — Tasarım Dokümanı

Tarih: 2026-08-21
Uygulama adı: **Kolay Yazdır** (pencere başlığı, kısayol adı ve kurulum adı bu)

## Amaç

Kırtasiyede günlük çıktı işini Windows'un yazdırma diyaloglarına girmeden bitiren tek pencerelik bir uygulama. Kullanıcı dosyaları seçer, birkaç düğmeye basar, önizlemede sonucu görür, yazdırır. Windows'un yazdırma ayarlarındaki gereksiz seçenekler ve dağınık arayüz tamamen gizlenir.

Hedef kullanıcı: dükkandaki çalışanlar. Bilgisayar bilgisi varsayılmaz.

## Kapsam

### Yapılacaklar

- Çoklu dosya seçimi: görsel (jpg/png/bmp/gif/webp/tiff), PDF, Word, Excel
- Varsayılan klasör İndirilenler; değiştirilebilir ve hatırlanır
- Kağıt boyutu: A4 / A5 / A3
- Renkli / siyah beyaz
- Dikey / yatay
- Önlü arkalı (çevirme kenarı otomatik) / tek yön
- Kağıt cinsi: sürücünün kendi listesinden ("Düz", "Kalın 1", …)
- Sayfaya yerleşim: 1 / 2 / 4 / 9 / 16 / 35
- Sayfaya sığdır seçeneği (varsayılan kapalı)
- Sayfa aralığı ve kopya sayısı
- Canlı önizleme
- Karanlık, yüksek kontrastlı tema
- GitHub Releases üzerinden sessiz otomatik güncelleme

### Yapılmayacaklar (bilinçli olarak dışarıda)

Yazıcı seçimi (dükkanda tek yazıcı var), tepsi seçimi, zımba/delgi, filigran, serbest ölçekleme yüzdesi, kitapçık modu, şifreli PDF açma, baskı geçmişi, bulut senkronizasyonu.

"PDF olarak kaydet" bu sürümde yok ama yerleşim modeli hazır olacağı için sonradan eklenmesi küçük bir iş.

## Teknoloji

| Karar | Seçim | Gerekçe |
|---|---|---|
| Dil / çatı | C# / .NET 8 / WPF | Windows yazdırma yığınına (DEVMODE, DeviceCapabilities) tam erişim; sürücüye özel kağıt cinsi isimlerini okuyabilen tek pratik yol |
| PDF ve sayfa render | PDFium | Olgun, hızlı, doğru; Chrome'un kullandığı motor |
| Word / Excel dönüşümü | Office COM → LibreOffice zinciri | Office varsa en sadık sonuç; yoksa her makinede kurulu olan LibreOffice |
| Dağıtım / güncelleme | Velopack | Tek exe kurulum, GitHub Releases'ten sessiz güncelleme, yönetici yetkisi istemez |
| Test | xUnit | Yerleşim motoru saf fonksiyon olduğu için yazıcısız test edilebilir |

.NET self-contained yayınlanır; makinelere ayrıca runtime kurmak gerekmez. Kurulum kullanıcı bazlıdır, yönetici şifresi istemez.

Reddedilen alternatifler: **Electron** — Chromium'un yazdırma katmanı kağıt cinsi seçimi sunmuyor ve önlü arkalı kontrolü güvenilmez; gereksinimlerin en kritik kısmında bizi native yardımcı yazmaya zorluyordu. **Python/Qt** — "tüm bilgisayarlara kur, otomatik güncellensin" tarafında düzgün bir çözümü yok.

## Mimari

Temel fikir: **önizleme ile baskı aynı kodu çalıştırır.** Yerleşim, çizimden ayrı bir saf veri modeli olarak hesaplanır; aynı model hem ekrana hem yazıcıya çizilir. Böylece önizlemenin çıktıdan sapması yapısal olarak imkansız hale gelir.

```
Dosyalar
   ├── Görsel ─────────────────┐
   ├── PDF ────────────────────┤
   └── Word/Excel → PDF'e çevir┘
                                ↓
                     SourcePage listesi
                     (boyut + render(dpi))
                                ↓
                     LayoutEngine  ← PrintSettings
                     (saf fonksiyon, çizim yok)
                                ↓
                       Sheet listesi
              (hangi sayfa, hangi dikdörtgene, kaç derece)
                          ↙          ↘
                 SheetRenderer    SheetRenderer
                   @ ~110 dpi       @ 600 dpi
                        ↓                ↓
                   Önizleme          Yazıcı (DEVMODE)
```

### Bileşenler

**DocumentLoader** — Dosya yolunu bir `SourceDocument`'a çevirir. Her `SourcePage` iki şey sunar: nokta cinsinden boyutu, ve istenen DPI'da bitmap üreten bir render metodu. Görseller WIC ile, PDF'ler PDFium ile açılır. Görselin DPI bilgisi yoksa 96 varsayılır (Windows'un davranışı).

**OfficeConverter** — Word/Excel dosyalarını PDF'e çevirir. Sırayla dener: kayıtlı Office COM otomasyonu, sonra LibreOffice (`soffice --headless --convert-to pdf`). LibreOffice yolu kayıt defterinden ve Program Files'tan aranır. Çevrilen PDF geçici klasörde dosya yolu + değişiklik tarihi anahtarıyla önbelleklenir; aynı dosya ikinci kez seçilince anında açılır.

**LayoutEngine** — Sistemin kalbi ve tek karmaşık parçası. Girdi: sayfa boyutları listesi + `PrintSettings`. Çıktı: `Sheet[]`. Hiçbir çizim yapmaz, hiçbir dış bağımlılığı yoktur, tamamen test edilebilir.

**SheetRenderer** — Bir `Sheet`'i verilen DPI'da bir hedefe çizer. Önizleme ve yazdırma bunu farklı DPI ile çağırır.

**PrinterCapabilities** — Win32 `DeviceCapabilities` ile sürücüden kağıt cinsi isimlerini (`DC_MEDIATYPENAMES`), dupleks desteğini (`DC_DUPLEX`) ve renk desteğini (`DC_COLORDEVICE`) okur.

**PrintJob** — DEVMODE'u kurar (kağıt boyutu, yön, dupleks modu, renk, kağıt cinsi, kopya) ve `PrintDocument` ile sayfa sayfa bastırır. Sayfalar tek tek render edilir, hiçbir zaman hepsi birden bellekte tutulmaz.

**SettingsStore** — `%AppData%` altında JSON. Varsayılan klasör ve en son kullanılan ayarlar saklanır; uygulama bir sonraki açılışta aynı ayarlarla gelir.

**UpdateService** — Velopack. Açılışta arka planda GitHub Releases'e bakar, yeni sürüm varsa indirir, bir sonraki açılışta uygulanır. Hata olursa sessizce geçer, kullanıcıyı asla bloklamaz.

## Yerleşim kuralları

Bu bölüm LayoutEngine'in tam davranışıdır; testler buradan yazılır.

### Sayfa akışı

1. Dosyalar listedeki sırayla birleştirilir, tek bir kaynak sayfa dizisi olur.
2. Sayfa aralığı bu birleşik diziye uygulanır (`1-5, 8, 11-13` biçimi; boşsa tümü).
3. Sayfalar yerleşim ızgarasına sırayla doldurulur.

### Izgara

| Seçenek | Izgara (dikey kağıtta) |
|---|---|
| 1 | 1 × 1 |
| 2 | 1 sütun × 2 satır |
| 4 | 2 × 2 |
| 9 | 3 × 3 |
| 16 | 4 × 4 |
| 35 | 5 sütun × 7 satır |

Yatay kağıtta satır ve sütun sayıları yer değiştirir. Doldurma soldan sağa, yukarıdan aşağıya. Son yaprakta boş kalan hücreler boş bırakılır.

### Ölçekleme

Her sayfa için, hücre boyutu `(cw, ch)` ve kaynak boyutu `(sw, sh)`:

1. **Otomatik döndürme** açıksa ve sayfanın yön oranı hücreninkiyle uyuşmuyorsa, sayfa 90° döndürülür (`sw` ve `sh` yer değiştirir).
2. `ölçek = min(cw/sw, ch/sh)`
3. **Sayfaya sığdır kapalıysa** (varsayılan): `ölçek = min(ölçek, 1.0)`. Yani gerçek boyut korunur, sadece taşıyorsa küçültülür, asla büyütülmez.
4. **Sayfaya sığdır açıksa**: ölçek olduğu gibi kullanılır, gerekirse büyütülür.
5. Sonuç hücrenin ortasına yerleştirilir.

Bu kural 1'li yerleşimde de çoklu yerleşimde de aynı şekilde işler; 1'li yerleşimde "hücre" kağıdın kendisidir.

### Kenar boşlukları

Yazıcının basamadığı fiziksel kenar payı `PageSettings.PrintableArea`'dan okunur. İçerik alanı, bu pay ile 5 mm'nin büyüğü kadar içeriden başlar. Çoklu yerleşimde hücreler arasında 3 mm boşluk bırakılır.

### Önlü arkalı

Çevirme kenarı kullanıcıya sorulmaz, yönden türetilir:

- Dikey → uzun kenardan çevir
- Yatay → kısa kenardan çevir

Arayüzde hangisinin seçildiği küçük gri yazıyla gösterilir.

Çoklu yerleşimle birlikte kullanıldığında yapraklar eşleşir: 4'lü + önlü arkalıda 1. yaprağın önü 1–4, arkası 5–8 sayfalarıdır. Toplam sayfa yaprağı tam doldurmuyorsa son arka yüz boş kalır.

**Otomatik dupleks yoksa** düğme "Önlü arkalı (elle)" olur: önce tüm ön yüzler sırayla basılır, sonra kağıtları ters çevirip tepsiye koymayı anlatan bir pencere çıkar, onaylanınca arka yüzler ters sırayla basılır (yüzü aşağı çıkaran yazıcılarda doğru sıra budur). Bu akış dükkandaki yazıcıda yerinde doğrulanmalıdır — yazıcıdan yazıcıya değişen tek davranış budur.

### Kopya ve harmanlama

Kopya sayısı sürücüye DEVMODE üzerinden verilir, harmanlama açıktır (1,2,3 – 1,2,3). Sürücü kopyalamayı desteklemiyorsa yapraklar uygulama tarafında tekrarlanır.

### Renk

Siyah beyaz seçiliyken önizleme de gri tonlamalı gösterilir; ekranda renkli görünüp gri çıkan bir sayfa olmaz.

## Arayüz

Tek pencere, iki sütun. Solda 250 px sabit genişlikte ayarlar, sağda önizleme.

Ayar sütunu yukarıdan aşağıya: dosya seç düğmesi, seçilen dosyalar listesi, kağıt boyutu, yön, renk, yüz, kağıt cinsi, sayfaya yerleşim, sayfaya sığdır, otomatik döndür, sayfa aralığı ve kopya. Altta büyük "Yazdır" düğmesi.

Seçimler açılır kutu yerine yan yana düğmelerle yapılır — tek tıkla değişir, seçili olan yüksek kontrastla (beyaz zemin, siyah yazı) belli olur.

Dosya listesi sürükleyerek sıralanabilir; her satırda dosya adı, türü ve sayfa sayısı görünür. Sürükle-bırak ile de dosya eklenebilir.

Önizleme alanı o anki yaprağı gösterir; ileri/geri okları ve "Yaprak 2 / 4 · arka" göstergesi vardır. Ayar değiştiğinde önizleme anında yenilenir.

Hiç dosya seçilmemişken önizleme alanı boş durumu gösterir: "Yazdırmak için dosya seç" yazısı ve altında dosya seçme düğmesi. Yazdır düğmesi bu haldeyken de tıklanabilir kalır ve tıklanınca dosya seçme penceresini açar — pasif, gri bir düğme göstermek yerine kullanıcıyı doğru yere yönlendirir.

Pencere başlığında yazıcı durumu görünür; yazıcı çevrimdışıysa veya kağıt bittiyse orası kırmızıya döner.

### Tema

Karanlık, yüksek kontrast. Zemin `#0A0A0A`, paneller `#141414`, kenarlıklar `#2E2E2E`, birincil metin `#FFFFFF`, ikincil metin `#A8A8A8`, vurgu `#FFD84D` (yazdır ve dosya seç düğmeleri). Seçili durum beyaz zemin + siyah yazı.

Metin sadece iki tonda kullanılır — daha soluk bir üçüncü ton yok. `#A8A8A8` zemin üzerinde 8,5:1 kontrast verir ve WCAG AAA'yı karşılar; bundan koyusuna inilmez.

## Hata yönetimi

| Durum | Davranış |
|---|---|
| Dosya açılamıyor / bozuk | Listede o satır kırmızı olur, sebebi yazar, işten çıkarılır; diğer dosyalar basılır |
| Word/Excel için dönüştürücü yok | LibreOffice'in adı geçen açık bir mesaj |
| Şifreli PDF | Açılamadığı belirtilir, iş iptal edilmez |
| Yazıcı çevrimdışı | Başlıkta kırmızı uyarı; yazdırmaya izin verilir, iş kuyruğa girer |
| Güncelleme kontrolü başarısız | Sessizce geçilir, hiçbir şey gösterilmez |
| Dönüşüm uzun sürüyor | Dosya satırında ilerleme göstergesi; arayüz donmaz |

Tüm dosya yükleme ve dönüştürme işlemleri arka planda çalışır. Arayüz hiçbir noktada kilitlenmez.

## Test stratejisi

Geliştirme TDD ile yürür.

**Birim testleri (yazıcı gerekmez, işin ağırlığı burada):** LayoutEngine'in tamamı — ızgara matematiği, ölçekleme ve sığdırma kuralları, otomatik döndürme, sayfa aralığı ayrıştırma, önlü arkalı yaprak eşleşmesi, tek sayılı sayfada boş arka yüz, kopya ve harmanlama, kenar boşluğu hesabı.

**Entegrasyon testleri:** DocumentLoader'ın örnek jpg/png/pdf dosyalarında doğru sayfa sayısı ve boyut vermesi; OfficeConverter'ın örnek bir .docx'i LibreOffice ile PDF'e çevirmesi.

**Manuel doğrulama:** Gerçek çıktı kalitesi, kağıt cinsi seçiminin sürücüye geçmesi, otomatik ve elle önlü arkalı akışı. "Microsoft Print to PDF" sanal yazıcısı ile duman testi.

## Dağıtım

Sürüm etiketi push edildiğinde bir GitHub Action `vpk pack` çalıştırır ve çıktıyı GitHub Releases'e yükler. Uygulama açılışta arka planda yeni sürüme bakar, varsa indirir, bir sonraki açılışta uygular.

İlk kurulum her bilgisayarda bir kez elle yapılır; sonrası kendiliğinden gider.

## Açık riskler

- **Elle önlü arkalı sayfa sırası** yazıcıya göre değişir; dükkandaki yazıcıda yerinde doğrulanacak.
- **Sürücü kağıt cinsi isimleri** her sürücüde bulunmayabilir. Sürücü liste vermezse DEVMODE'un standart kağıt cinsi sabitleriyle "Düz / Kalın" eşlemesine düşülür.
- **Office COM otomasyonu** bazı Office sürümlerinde arka planda çalışırken sorun çıkarabilir; bu yüzden LibreOffice her zaman geçerli bir yedek olarak kalır ve tercih sırası ayarlardan değiştirilebilir.
