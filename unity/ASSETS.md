# Üretilen 3D modeller — kabul ve işleme boru hattı

Summer Engine'in Studio sekmesindeki AI üreteci `.glb` veriyor. Gelen dosya **oyuna hazır
değil** ve elle düzeltilmeyecek: her model aynı iki komuttan geçecek.

İlk gerçek ölçüm (bir ev modeli):

| | üretecin verdiği | işlendikten sonra |
|---|---|---|
| Dosya | 12.7 MB | **104 KB** |
| Üçgen | 40.000 | **1.702** |
| Doku | 1 × büyük | 1 × 128 px |

Yani 123 kat küçülüyor ve görünüş oyun kamerasından ayırt edilemiyor. Ama işlenmemiş hâliyle
31 bina ~380 MB ve milyonlarca üçgen demek — kullanılamaz.

## Boru hattı

Kurulum bir kere: `npm install -g @gltf-transform/cli`

```bash
# 1) buda, kaynaştır, dokuyu küçült
npx gltf-transform optimize ham.glb ara.glb --texture-size 128 --compress false --simplify false

# 2) üçgen azalt
npx gltf-transform simplify ara.glb temiz.glb --ratio 0.12 --error 0.002
```

`--ratio` tek ayar düğmesi. `0.04` denedik: 1.702 üçgen, siluet duruyor ama yüzey eriyor.
**`0.12` civarı daha iyi denge** — bina şehirde birer ikişer bulunuyor, yüzlerce kez
tekrarlanan bir nesne değil, o yüzden 3–4 bin üçgen tamamen makul. 200 binalık şehir 800 bin
üçgen eder, instancing ile sorun değil.

## Unity içe aktarma — erimiş görünümün gerçek sebebi

Azaltma sonrası yüzey dalgalı görünüyorsa mesh suçlu değil, **normaller** suçlu: düşük
poligonlu form keskin ama yumuşak normaller onu erimiş gösteriyor. Model içe aktarılırken:

- **Normals: Calculate**, **Smoothing Angle: 30** → pahlar keskinleşir, faset okunur.
- **Import Materials** kapalı; malzemeyi biz kuruyoruz (aşağıya bakın).
- **Read/Write** kapalı, **Mesh Compression** medium.

## Kabul şartları — bir model bunları geçmeden `Assets/`'e girmez

Ajan her gelen modeli ölçecek ve raporlayacak:

1. **Üçgen ≤ 4.000.** Aşıyorsa `--ratio`'yu düşürüp tekrar geçir.
2. **Dosya ≤ 300 KB.**
3. **Zemine oturuyor:** dünya uzayında `bounds.min.y` = 0 ± 0.02.
   **Dikkat:** ham vertex sınırlarına bakmak yanlış sonuç verir — node dönüşümünü uygulayın.
   İlk modelde ham veri "zeminde değil" dedi, dünya uzayında ise tam oturuyordu.
4. **Tabanı ortalı:** `bounds.center.x` ve `.z` = 0 ± 0.05.
5. **Taban plakası YOK.** Üreteç istenmese de kaide koyuyor; oyunun kendi zeminiyle üst üste
   binip binayı havada duran bir platform gibi gösterir. Varsa kırpılacak.
6. **Ölçek normalize:** kare 4 m. Çarpan tek bir sabitte tutulacak, model başına elle
   ayarlanmayacak. Ayak izleri artık veri tablosunda (`BuildingDef.Size`) ve oyun bunları
   gerçekten uyguluyor: `santral`, `hastane`, `kisla`, `tersane` **2×2**, `pazar` **2×1**,
   gerisi 1×1. Çok kareli bina yerleşimde dört (veya iki) boş kare ister, modeli o alana
   ölçeklenir, imleç tüm ayak izini gösterir.

## Dosya adları — teslimat sözleşmesi

Üreteçten inen her `.glb`, oyundaki yapı kimliğiyle adlandırılır ve tek klasörde teslim
edilir; ajan gerisini (boru hattı + `StreamingAssets/Models/buildings/` + doğrulama)
kendisi yapar. Kimlikler:

`konut · toplukonut · tarla · degirmen · firin · ambar · tayinlama · ocak · islik · depo ·
dokuma · santral · kuyu · sukemeri · aritma · pazar · borsa · klinik · hastane · okul ·
kutuphane · hamam · tapinak · matbaa · park · anit · karakol · kontrol · kisla · tersane`

Eşleme tuzakları: T. KONUT → `toplukonut`, TAYIN → `tayinlama`, İŞLİK → `islik`,
KEMER → `sukemeri`, DOKUMA → `dokuma`. Türkçe karakter yok, hepsi küçük harf.

Çalışma anı tarafı hazır: `World/BuildingModels.cs` bu klasördeki her dosyayı kimliğiyle
yükler, sınırlarını ölçüp kareye oturtur ve prosedürel formu devre dışı bırakır — model
başına kod değişikliği gerekmez.

## Kabul edilmiş istisnalar

Üç model, hata payı ne kadar gevşetilirse gevşetilsin üçgen bütçesine inmiyor — kopuk küçük
parçalardan (kafes direk, tezgâh kalabalığı, pencere oyukları) sadeleştirici bileşen sınırı
aşamıyor. Şehirde birer ikişer bulundukları için kabul edildiler:

| model | üçgen | dosya | sebep |
|---|---|---|---|
| pazar | 12.750 | 816 KB | tezgâhlar + tenteler |
| hastane | 8.140 | 542 KB | pencere oyukları |
| santral | 7.364 | 500 KB | kafes direk |

Gelecek üretimlerde prompt'a "no thin lattice, fewer chunkier stalls" eklenirse bunlar da
bütçeye iner.

## Üreteç prompt'unda düzeltilecek üç şey

İlk denemede üreteç şunları ıskaladı, prompt'lar buna göre güncellenecek:

- **Oran sayıyla verilecek.** "İki katlı ev" dedik, saat kulesi geldi — taban ile yükseklik
  neredeyse eşitti. Bundan sonra: *"footprint twice as wide as the building is tall"*.
- **Taban plakası daha sert reddedilecek:** *"absolutely no base slab, no plinth, no terrain
  disc — the mesh must end where the walls meet the ground."*
- **Düz renk ısrarla istenecek.** Üreteç dokulu veriyor; 12 MB'ın tamamı o dokuydu. Düz renk
  gelirse materyal rengiyle bina çeşitlendirmek, ideoloji propları ve teknoloji kademeleri
  için tonlamak mümkün olur. Doku gelirse bu kapı kapanır. Gelmeye devam ederse 128 px'e
  indirip kabul ediyoruz, ama önce isteyelim.
