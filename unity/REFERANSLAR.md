# Referans kod tabanları ve lisans durumu

Sıfırdan icat etmemek için okunacak kaynaklar. **Lisans ayrımına dikkat:** aşağıdakilerin
çoğu GPL ailesinden. GPL kodu okumak, öğrenmek ve modelini kendi kelimelerinle yeniden
yazmak serbesttir. Kodu kopyalayıp kapalı kaynak ticari bir oyuna koymak ise tüm oyunu
GPL yapmanı zorunlu kılar. Bu depoda kural: **oku, öğren, kendin yaz.**

## Kapalı — kaynak alınamaz

**Cities: Skylines / CS2.** Oyunu satın almak kaynak kodu vermez; derlenmiş DLL olarak
gelir. Decompile edip kod taşımak EULA ihlali ve telif ihlalidir. Meşru olan tek yol,
yayınlanmış modding API'sine karşı mod yazmak ve GitHub'daki izin verici lisanslı modları
incelemektir — oyunun kendi kodunu değil.

## Açık — okumak için

| Proje | Dil | Lisans | Ne için okunur |
|---|---|---|---|
| **Micropolis** (orijinal SimCity) | C/C++ | GPLv3 | Türün temel modeli: imar talebi (RCI), arazi değeri, kirlilik yayılımı, trafik baskısı, nüfus göçü. Kompakt ve okunabilir. **En değerli kaynak.** |
| **OpenCity** | C++ | GPLv2 | 3D şehir kurucu; kamera, ızgara, render düzeni |
| **Lincity-NG** | C++ | GPL | Kaynak zincirleri ve ekonomik denge |
| **Citybound** | Rust | açık kaynak (sürüm doğrulanmalı) | Ajan bazlı mikro simülasyon mimarisi; geliştirmesi durmuş ama fikirleri değerli |
| **A/B Street** | Rust | **doğrulanmalı** | Gerçek şehirlerde trafik ve yol ağı simülasyonu. Lisansı teyit edilmeden kod alınmayacak |

## Meşru kısayollar

- **Unity Asset Store kitleri.** Izgara yerleştirme, yol çizme, izometrik kamera için hazır
  ticari lisanslı paketler. Bir haftalık altyapı işini bir güne indirir; bizim ayırt edici
  sistemlerimize vakit kalır.
- **Unity paketleri:** Splines (yol eğrileri), AI Navigation, Entities/Burst (kalabalık).

## Neyi kendimiz yazıyoruz ve neden

MEŞRUİYET'i ayıran şey şehir kurma katmanı değil — o çözülmüş bir problem. Ayıran şey
bakanların çarpıttığı bilgi katmanı, tampon mekaniği, alacaklıların yasa yuvası işgali ve
hesap verme oturumu. Bunların hiçbiri mevcut hiçbir şehir oyununda yok, dolayısıyla
kopyalanacak bir kaynak da yok. Hazır bir kod tabanını devralmak işin en kolay parçasını
verir, karşılığında gezinemeyeceğimiz büyüklükte bir yük bindirir.

Kural: **simülasyon modelini Micropolis'ten öğren, kodu C# ile kendin yaz, altyapıyı satın al.**
