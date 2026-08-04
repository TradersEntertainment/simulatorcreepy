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
7. **Yükseklik veriden gelir, üreteçten değil.** `BuildingModels` her modelin dünya
   yüksekliğini `Storeys × katYüksekliği` hedefine çeker (konut 2.6 m/kat, kamu 3.1 m/kat —
   prosedürel formlarla aynı kural). Y çarpanı [0.55, 1.8] aralığına kısılır; `Storeys = 0`
   olanlar (tarla, park) kendi oranını korur. Çok kareli binada taban parselle büyür,
   yükseklik büyümez. Yani üretecin binayı yanlış boyda vermesi kabulü etkilemez —
   ama aşırı basık/aşırı sivri gelen model kısıtlamaya takılıp yine de yanlış okunur,
   oranı prompt'ta sayıyla istemeye devam edin.

## Dosya adları — teslimat sözleşmesi

Üreteçten inen her `.glb`, oyundaki yapı kimliğiyle adlandırılır ve tek klasörde teslim
edilir; ajan gerisini (boru hattı + `StreamingAssets/Models/buildings/` + doğrulama)
kendisi yapar. Kimlikler:

`konut · toplukonut · tarla · degirmen · firin · ambar · tayinlama · ocak · islik · depo ·
dokuma · santral · kuyu · sukemeri · aritma · pazar · borsa · klinik · hastane · okul ·
kutuphane · hamam · tapinak · matbaa · park · anit · karakol · kontrol · kisla · tersane`

Eşleme tuzakları: T. KONUT → `toplukonut`, TAYIN → `tayinlama`, İŞLİK → `islik`,
KEMER → `sukemeri`, DOKUMA → `dokuma`. Türkçe karakter yok, hepsi küçük harf.

**Varyantlar:** `kimlik-2.glb`, `kimlik-3.glb`... aynı binanın ek modelleridir; şehirde o
binadan her yeni koyuşta sıradaki varyant gelir ve tur tamamlanınca başa döner (kuruluş
sırası sabittir — bina sonradan model değiştirmez). İlk kullanım: `tapinak.glb` cami,
`tapinak-2.glb` kilise, `tapinak-3.glb` sinagog. Aynı mekanizma başka binalara da açık
(ör. konut çeşitliliği).

## Araçlar — ikinci teslimat sözleşmesi

`StreamingAssets/Models/vehicles/` altına `otomobil.glb`, `yukarabasi.glb` veya `fayton.glb`
düşürülür; sırasıyla prosedürel sedan, kamyonet ve at arabasının yerine geçer. Boru hattı
binalarla aynı (renk-uzayı sapağı dahil). Farklar (`World/VehicleModels.cs`):

- Araçlar kalabalığın instanced çizim yolunda akar; doku okunmaz. Bu yüzden model tek mesh'e
  kaynatılır ve **128 px dokusu vertex rengine örneklenir**; kendi boyasını taşıdığı için
  bucket tonu yerine nötr malzemeyle çizilir.
- **Uzun eksen +Z'ye çevrilir** (burun kuralı — probe ölçerek doğruluyor), boy ve zemin
  çizgisi yerine geçtiği prosedürel aracın kutusuna oturtulur.
- Üçgen bütçesi binadan sıkı düşünülmeli: aynı model onlarca kez sahnede.

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
| islik | 5.010 | 293 KB | tezgâh + taş blokları (0.005 oranında bile 4.104'ün altına inmiyor) |
| tersane | 7.090 | 390 KB | gemi gövdesi + rampa parçaları (taban 6.746; 2×2 bina, santral emsali) |

Gelecek üretimlerde prompt'a "no thin lattice, fewer chunkier stalls" eklenirse bunlar da
bütçeye iner.

## Renk uzayı tuzağı — son partide çıktı (ocak/islik/kontrol/tersane)

Üreteç bu partide 4096×4096, sharp'ın okuyamadığı renk-uzaylı PNG doku verdi;
`gltf-transform optimize --texture-size 128` **"colourspace: parameter space not set"**
hatasıyla düşüyor. Çözüm: dokuyu çıkar, GDI+ ile 128 px'e indir, geri koy, sonra boru
hattının kalanını `--texture-compress false` ile çalıştır. Scriptler ajan scratchpad'inde
(`tex-extract.mjs`, `recenter.mjs`, `measure.mjs`) — gerekirse yeniden yazılması beş dakika.
`recenter.mjs` ayrıca XZ merkezini tam sıfıra, tabanı tam zemine çeker; bu parti ±0.07'ye
kadar kaymış geliyordu.

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
