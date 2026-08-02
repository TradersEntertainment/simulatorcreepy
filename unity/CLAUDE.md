# MEŞRUİYET — Unity projesi çalışma kuralları

Bu dosya, projede çalışan yapay zekâ ajanı içindir. Tasarımın kendisi `../PROMPT-CITY.md`
ve `../PROMPT-CITY-COOP.md` dosyalarındadır; görsel referanslar `../reference/` altındadır.
Buradaki kurallar **nasıl çalışılacağını** anlatır.

## Temel kural: kullanıcı kod yazmaz, Unity açmaz, Play'e basmaz

Her değişiklikten sonra doğrulamayı sen yaparsın:

```powershell
.\agent\loop.ps1 -Scenario smoke
```

Bu derler, oyunu başlatır, `AgentBridge` üzerinden oynar, ekran görüntüsü alır, logları tarar.
Çıkış kodu 0 değilse iş bitmemiştir. **Kullanıcıya "çalışıyor mu bak" deme** — kendin bak,
sonra ekran görüntüsü ve iki paragraf özetle rapor ver.

Kaynak `unity/Assets` altındadır; Unity proje klasörü (`cityGame/`) çalışma kopyasıdır ve
`loop.ps1` her koşuda senkronlar. **Proje klasöründeki dosyaları elle düzenleme** — bir sonraki
koşuda üzerine yazılır.

## Mimari kuralları

- **Sahne ve prefab elle yazılmaz.** `.unity` ve `.prefab` dosyaları GUID referanslı YAML'dır;
  metin olarak üretmek kırılgandır. Build Settings'te tek bir boş sahne durur, dünyayı
  `Bootstrap.cs` koddan kurar. Prefab gerekiyorsa çalışma anında oluştur.
- **Veri tabloları düz C# statik sınıflarıdır** (`Content/Buildings.cs` gibi), ScriptableObject
  asset'i değil. Asset üretmek editör script'i gerektirir ve versiyon kontrolünde gürültü yapar.
  Yeni bina/yasa/olay eklemek tek satırlık veri değişikliği olmalı.
- **Performans veri düzeninden gelir, dilden değil.** Kalabalık ve trafik gibi kütlesel
  simülasyonlar `NativeArray<struct>` üzerinde Job System + Burst ile koşar. Ajan başına
  `MonoBehaviour` **yasak**. Tekrar eden geometri `RenderMeshInstanced` ile çizilir.
- **Uzaktaki mahalle istatistikseldir.** Kamera yakınındaki ajanlar tek tek canlandırılır,
  uzaktakiler toplam sayı olarak simüle edilir. Büyük dünya çok ajan demek değildir.
- **Gerçek değer ile gösterilen değer ayrıdır.** `GameState` gerçeği tutar, `Reporting`
  bakanların çarpıttığı hâlini üretir. **Arayüz asla `GameState`'ten okumaz.** `AgentState`
  ise gerçeği okur — ajan yalanın işe yarayıp yaramadığını görebilmelidir.

## Ajan köprüsü

`Assets/Scripts/Agent/AgentBridge.cs` yalnızca debug/editör build'lerinde derlenir ve
`127.0.0.1:8787` üzerinde satır bazlı JSON konuşur: `ping`, `state`, `press`, `click`,
`build`, `endturn`, `shot`, `quit`. Yeni bir sistem eklediğinde:

1. `AgentState.DumpJson()` çıktısına ilgili alanları ekle — göremediğin şeyi test edemezsin.
2. Yeni bir UI düğmesi eklediğinde ona `name` ver ki `click` ile basılabilsin.
3. Uzun süren bir iş varsa `AgentState.TurnIdle` benzeri bir bayrakla bitişini bildir.

## Çalışma ritmi

İşi oynanabilir dilimler hâlinde götür. Her dilim sonunda: `loop.ps1` temiz geçmiş olacak,
ekran görüntüsü alınmış olacak, ve commit atılmış olacak. Konteyner ya da makine kapanırsa
kaybolmaması için **her dilim sonunda push et**.

Önerilen sıra:
1. Izgara, izometrik kamera, koddan kurulan şehir, tur döngüsü
2. Kaynaklar, ihtiyaçlar, mahalle hoşnutsuzluğu, tedarik zincirleri
3. Bakanlar ve `Reporting` çarpıtma katmanı
4. Yasalar, kararnameler, meclis, seçimler
5. Dış dünya: Mersa, alacaklılar, mülteciler
6. Kalabalık ve trafik (Burst + instancing), ideoloji propları
7. Arayüz (UI Toolkit; USS'i `../reference/src-*.html` içindeki CSS'ten türet)
8. Çöküşler, sonlar, hesap verme oturumu
9. Co-op ve WebGL

## Rapor biçimi

Kullanıcı kod okumaz. Her dilim sonunda: ne çalışıyor, ne çalışmıyor, ne karar bekliyor —
üç kısa paragraf ve ekran görüntüsü. Teknik ayrıntıyı ancak sorulursa aç.
