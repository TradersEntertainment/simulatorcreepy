# MEŞRUİYET — CO-OP (Unity, C#)

Kampanyanın üzerine kurulan çok oyunculu mod. **Bu dosya `PROMPT-CITY-COOP.md`'nin yerini alır** —
o dosya Godot/GDScript için yazılmıştı ve artık geçersiz.

Tek kişilik oyun çalışıyor ve aşağıdaki sınıflar mevcut. Co-op bunları **yeniden yazmaz**, yalnızca
bir yerinden bağlanır.

---

## 1. Temel fikir: tek bir kaynağı değiştiriyoruz

Tek kişilik oyunda bakanların yalanı `Core/Reporting.cs` üzerinden akıyor. Her sayı
`Reporting.BiasFor(Domain)` çarpanından geçiyor, o da `Distortion.Bias(minister, state)`
formülünden geliyor.

**Co-op'ta o formülün yerini bir insan alıyor.** Mimaride değişen tek şey bu.

```csharp
// Core/Reporting.cs — bugün
public static float BiasFor(Domain domain) => Distortion.Bias(G.Cabinet.Of(domain), G);
```

Bunu bir arayüzün arkasına alın:

```csharp
public interface IReportSource
{
    // Bu bakanlığın bu tur bildirdiği değer. Gerçek değeri alır, bildirileni döner.
    float Report(Domain domain, ReportLine line, float trueValue);
    bool  HasSubmitted(Domain domain);
}

// Üç uygulama, tek yol:
//   FormulaSource — bugünkü Distortion.Bias. Tek kişilik oyun ve boş koltuklar.
//   HumanSource   — ağdan gelen oyuncu raporu.
//   BotSource     — FormulaSource + kişilik profili (aşağıda).
public static class Reporting
{
    public static IReportSource Source = new FormulaSource();
    // Stock/Flow/Population/Grievance/Buffer/Product hepsi Source üzerinden geçer.
}
```

**Kural:** `Reporting`'in dışındaki hiçbir yer `Source`'u tanımaz, ve arayüz hâlâ asla
`GameState`'ten okumaz. `AgentState` gerçeği okumaya devam eder — yalanın işe yarayıp
yaramadığını ancak böyle ölçebiliriz.

**Rapor satırları.** Bir bakanın neyi bildirdiği zaten tanımlı: `Ministers.DomainOf(Res)` hangi
kaynağın hangi bakanlığa ait olduğunu söylüyor. `ReportLine`, o eşlemeye ek olarak zincir
aşamalarını, tamponu ve hoşnutsuzluğu da kapsayan bir enum olsun. Bir insan raporu bu satırların
sözlüğüdür, başka bir şey değil.

---

## 2. Roller ve koltuklar

**2–6 oyuncu. Bir VALİ, beş BAKAN:** `Domain.Maliye`, `Tarim`, `Guvenlik`, `Imar`, `Halk`.

- **Bakan** yalnızca kendi alanının **gerçek** değerlerini görür. Hazineyi, eksenleri, başka
  bakanlığın verisini görmez.
- **Vali** yalnızca beş raporu görür ve bütün kararları tek başına verir: inşa, kararname, yasa,
  bütçe, meclis, atama, seçim. Bugünkü tek kişilik arayüzün tamamı valinin arayüzüdür.

**Boş koltuklar bota gider, harita hiç küçülmez.** 1 insan + 5 bot geçerli bir oyundur ve asıl
test yolu budur. Botlar `FormulaSource` üzerine kişilik biner: `ŞİŞİRİCİ`, `SAKLAYICI` (zincir
tıkanıklığını sağlam toplamın arkasına gizler), `ALARMCI`, `DÜRÜST AMA BECERİKSİZ`, `YALAKA`.
`Content/Ministers.cs` zaten isim ve `Loyalist` bayrağı tutuyor; profil oraya bir alan olarak
eklenir.

**Vali de bot olabilir.** `BotGovernor` yalnızca `Reporting` üzerinden okur — asla `GameState`'ten.
Bunu kodda `Debug.Assert` ile koruyun: bot vali gerçeği görürse aldatma sistemi sessizce devre
dışı kalır ve kimse fark etmez.

---

## 3. Tur akışı

`Sim/TurnResolver.cs` bugün `BeginTurn()` çağrılınca turu çözüyor ve `TurnCompleted` yayınlıyor.
Co-op araya iki faz sokar:

1. **BAKAN FAZI** (eşzamanlı, 90 sn, host ayarlar). Her bakan kendi gerçek sayılarını görür ve
   raporunu yazar. Ekranda iki sütun: **GERÇEK** (yalnızca o görür) ve **RAPOR** (valiye gidecek).
   Erken gönderilebilir; süre dolarsa mevcut hâli gider. Botlar 5–40 sn arası rastgele gecikmeyle
   gönderir ki faz boş hissettirmesin.
2. **VALİ FAZI.** Beş telgraf düşer. Vali oynar ve turu bitirir. `TurnResolver.BeginTurn()` yalnızca
   burada çağrılır.
3. **ÇÖZÜMLEME.** Herkes aynı animasyonu izler. Bir şeyin yanlış olduğu burada anlaşılır.

`TurnResolver.Idle` faz denetleyicisinin senkron noktasıdır; değiştirmeyin.

---

## 4. Gizli hedefler

Her bakana — insan ya da bot — kapalı bir hedef dağıtılır. En az 14 tanesi olsun:
*"kendi fraksiyonun 60 üzerinde bitsin"*, *"hiçbir bakan görevden alınmasın"*, *"en az iki seçim
ertelenmiş olsun"*, *"hazine 60. turda 5000 ₺ üzerinde olsun"*, *"şehirde hiç kontrol noktası
kurulmasın"*.

**Şehir çökerse herkes kaybeder, hedefler dahil.** Hain yok, sabotajcı yok. Herkes şehrin ayakta
kalmasını gerçekten istiyor; herkesin ayrıca kendi derdi var; bu yüzden herkes raporunu birazcık
eğiyor. Arayüzde hiçbir yerde sabotaj ima edilmeyecek.

---

## 5. İletişim

- **Resmî Telgraf** — tek açık kanal. Kalıp ifade çipleri (`durum tatminkârdır`,
  `tedbir alınmıştır`, `zât-ı âlinizi meşgul etmeye değmez`, `mesele tetkik edilmektedir`) artı
  serbest metin. En az 30 kalıp. Her telgraf mühürlenir, arşivlenir ve **hesap verme oturumunda
  kelimesi kelimesine alıntılanır.**
- **Özel Kanal** — iki bakan arasında. Vali kanalın *var olduğunu* küçük bir ikonla görür,
  içeriğini asla görmez. Botlar da hedefleri örtüştüğünde kanal açar.
- **Vali Fermanı** — tur başına bir açık duyuru.

---

## 6. Final — hesap verme oturumu

`UI/FinalSession.cs` zaten var ve tek kişilik oyunda gerçek değerleri bildirilenlerin yanına
koyuyor. Co-op'ta bu ekran modun bütün anlamı hâline gelir; şunları ekleyin:

1. **Sapma tablosu** — oyuncu başına, tur başına: bildirilen ve gerçek, ömür boyu ortalamasıyla.
   `Minister.AverageBiasPercent` zaten tutuluyor, insan raporları için de aynı alanı doldurun.
2. **Telgraf alıntıları** — herkesin en kötü telgrafı, kendi kelimeleriyle, o anki gerçek sayının
   yanında.
3. **Gizli hedefler açılır.**
4. **Özel kanallar tamamen yayımlanır.** Bunu oyunun başında duyurun — insanlar kaydın açılacağını
   bilerek yazsın. Yine de yazacaklar.
5. **Eksen izi** tam ekran, her geri dönüşsüz turda valinin hangi bakanın raporuna dayanarak karar
   verdiği işaretli.

Kavgayı komedi için oynatın, son ekranı buz gibi bırakın.

---

## 7. Ağ

**Godot'un `WebSocketMultiplayerPeer`'ini unutun** — o eski dokümandan kalma.

Sunucu tarafını sıfırdan tasarlamıyoruz: aynı hesapta çalışan **FETİH** projesinde
(`github.com/TradersEntertainment/gfbgamereal`, `server/` klasörü) bu iş bir kere çözülmüş ve
canlıda duruyor. Oradan **altyapıyı** alıyoruz, yetki modelini değil. Dosyaları okuyun:
`server/wrangler.jsonc`, `server/worker.js`, `server/liveroom.js`, `server/DEPLOY.md`,
`.github/workflows/deploy-worker.yml`.

### Neyi aynen alıyoruz

- **Lobi kodu → Durable Object örneği.** `env.LIVE_ROOM.idFromName(KOD)` ile aynı koda bağlanan
  herkes aynı örneğe düşer. Bizde binding adı `LOBBY` olsun, yol `/lobby/<KOD>`.
- **`new_sqlite_classes` — bu kritik.** FETİH'in `wrangler.jsonc`'undaki not ücretsiz plan
  sorusunun cevabı: Durable Object ücretsiz planda **SQLite destekli sınıf** ister.
  ```jsonc
  { "migrations": [ { "tag": "v1", "new_sqlite_classes": ["Lobby"] } ] }
  ```
  `new_classes` yazarsanız ücretsiz planda deploy reddedilir. Daha önce "kotayı panelden
  doğrula" demiştim; cevap buymuş.
- **Origin allowlist.** `worker.js` yalnızca bilinen origin'lerden gelen upgrade'i kabul ediyor,
  gerisine 403. Aynısını yapın; bizim listemiz Pages alan adı + `localhost`.
- **Kalıcılık.** `state.blockConcurrencyWhile()` içinde son anlık görüntüyü `storage`'dan yükleyin,
  değişince `storage.put()`. FETİH'te bu, ana oyuncunun bilgisayarı kapansa bile odanın yaşamasını
  sağlıyor. Bizde karşılığı şu: **host çökerse maç kaybolmaz**, başka bir istemci host olup son
  anlık görüntüden devam eder. Röle olmamıza rağmen bu değerli, ucuz, ve alın.
- **Zamanlayıcı: gerçek zamanlı tick YOK.** FETİH `nextAlarmAt(room)` ile bir sonraki mutlak
  son tarihi hesaplayıp `state.storage.setAlarm(at)` kuruyor; alarm çalınca fazı ilerletiyor.
  Bizim 90 saniyelik bakan fazımız için birebir doğru desen. **Ayrıca zorunlu:** host susarsa
  faz sonsuza kadar açık kalmasın diye son tarihi sunucu uygular, host değil.

### Neyi almıyoruz, ve neden

FETİH'te oyun mantığının tamamı JavaScript'e taşınmış (`liveroom-core.js`, 65 KB) çünkü o oyun
rekabetçi ve gerçek zamanlı — sunucu hakem olmak zorunda. **MEŞRUİYET co-op'u işbirlikçi.**
Kuralları ikinci kez C#'tan JS'e çevirmenin bedeli, kazandıracağı şeyden büyük. Bu yüzden:

- **Yetki host istemcide.** Vali'nin istemcisi `GameState`'i tutar ve turu çözer.
- **Durable Object aptal bir röledir** artı bir son-tarih saati artı bir yedek. Oyun kuralı bilmez.
- Arena moduna gelince bu karar yeniden tartışılır — orada rekabet var. `ARENA-PLAN.md` §9'a bakın.

### Yine de FETİH'in en iyi fikrini alın: üç katman

`liveroom-core.js` (platform bağımsız) → `liveroom.js` (DO adaptörü) → `tools/dev/live-server.mjs`
(aynı çekirdeği kullanan yerel Node sunucusu). Bu ayrım sayesinde ağ mantığı **deploy etmeden**
test edilebiliyor. Bizde aynısı:

- `web/lobby-core.js` — koltuk listesi, faz saati, mesaj yönlendirme. Platform bağımsız.
- `web/lobby-do.js` — Durable Object adaptörü, WebSocket sahipliği.
- `web/dev-server.mjs` — aynı çekirdeği çalıştıran yerel Node sunucusu. **Geliştirme boyunca
  Cloudflare'e hiç dokunmadan altı koltuklu lobi test edilir.**

### Hibernation — bir düzeltme

FETİH `server.accept()` kullanıyor, yani DO bellekte kalıyor ve süre faturalanıyor. Sıra tabanlı
oyunumuzda fazlar arası uzun boşluklar var, o yüzden **Hibernation API tercih edilir**:
`state.acceptWebSocket(server)` + `webSocketMessage()` / `webSocketClose()` metotları.
Dikkat: hibernation'da `this.conns` gibi bellekteki Map'ler hayatta kalmaz —
`state.getWebSockets()` ile listelenir, koltuk numarası `serializeAttachment()` ile sokete iliştirilir.
Bu farkı atlarsanız oyuncular sessizce düşer. Hibernation zorlarsa FETİH'in `accept()` deseni
çalışan bir geri çekilme noktasıdır.

### Unity tarafı

- **Taşıma:** masaüstünde `System.Net.WebSockets.ClientWebSocket`. **WebGL'de bu sınıf çalışmaz** —
  tarayıcıda ham soket yok. `.jslib` eklentisiyle tarayıcının `WebSocket` nesnesini saran ince bir
  katman yazın, arkasına aynı C# arayüzünü koyun. WebGL'de en çok vakit yakan tuzak budur.
- **Netcode for GameObjects kullanmayın.** Tur başına birkaç yüz bayt taşıyan, gizli bilgili,
  sıra tabanlı bir oyun için ağır ve ters bir araç.
- **Rol görünümü bir güvenlik sınırıdır.** `RoleView.For(seat)` mesajı kurarken filtreler; arayüzde
  gizlemek yetmez, herkes hata ayıklayıcıyı açabilir. Testte doğrulayın: bir bakan istemcisine
  giden sözlükte kendi alanı dışında hiçbir anahtar bulunmasın.
- **Yeniden bağlanma** lobi koduyla aynı koltuğa. Kopan oyuncunun yerini o tur bot alır, bağlanınca
  koltuğu geri verir. Maç asla durmaz.
- **Dosyalar:** `Assets/Scripts/Net/` altında `NetManager.cs`, `RoleView.cs`,
  `CoopTurnController.cs`, `Transport/DesktopSocket.cs`, `Transport/WebSocket.jslib`.

### Deploy — elle uğraşmayın

FETİH'te deploy tek seferlik kurulumdan sonra kendiliğinden oluyor; aynısını kurun.
`.github/workflows/deploy-worker.yml` dosyasını örnek alın: yalnızca `actions/checkout` ve
`actions/setup-node` kullanıyor, üçüncü taraf action yok, sonra `npx --yes wrangler@4 deploy`.
Gereken iki depo secret'ı: `CLOUDFLARE_API_TOKEN` ve `CLOUDFLARE_ACCOUNT_ID` — ikisinin nasıl
alınacağı `server/DEPLOY.md`'de adım adım yazıyor, kullanıcı o adımları bir kere yapmış.
`web/**` altına push edilince Worker kendiliğinden güncellensin.

---

## 8. Ajan döngüsüyle nasıl test edilir

`AgentBridge` bugün tek koltuk varsayıyor. Co-op için:

1. `Cmd` yapısına `seat` alanı ekleyin; komut o koltuk adına çalışsın.
2. Yeni komutlar: `{"cmd":"report","seat":2,"line":"tahil","value":64}` ve
   `{"cmd":"submit","seat":2}`.
3. `AgentState.DumpJson()` çıktısına koltuk başına **bildirilen** ve **gerçek** değerleri
   yan yana ekleyin — ölçemediğiniz yalanı test edemezsiniz.
4. `loop.ps1`'e `-Scenario coop` ekleyin: altı koltuk açılır, biri kasıtlı yalan söyler, on tur
   oynanır, ve son kontrol şudur: **valinin kararları yalanla değişti mi?** Değişmediyse aldatma
   döngüsü kapanmamış demektir ve sistem sadece süstür.

Bu senaryo yeşil geçmeden co-op bitmiş sayılmaz.

---

## 9. Yapım sırası

1. `IReportSource` ayrımı + `FormulaSource`. Davranış birebir aynı kalmalı — tek kişilik oyun
   hiçbir fark hissetmemeli. **Bu dilim tek başına test edilir ve tek başına commit edilir.**
2. `BotSource` + beş kişilik profili. Hâlâ tek oyunculu, hâlâ ağsız, ama artık bakanlar karakterli.
3. Bakan ekranı (GERÇEK / RAPOR iki sütun) ve `HumanSource`, **yerel hot-seat olarak**. Ağ yok.
4. Gizli hedefler ve telgraf sistemi.
5. Ağ: önce `web/dev-server.mjs` (yerel Node, Cloudflare yok) ile altı koltuk; çalışınca
   aynı çekirdeği Durable Object'e tak. Masaüstü taşıma.
6. WebGL taşıma katmanı ve dağıtım.
7. Hesap verme oturumunun co-op sürümü.

İlk üç dilim ağ kodu olmadan bitiyor — yani co-op'un asıl fikri (insan yalanı) ağ hiç kurulmadan
oynanabilir ve test edilebilir hâle geliyor. Sırayı bozmayın.

---

## 10. Görsel

`reference/ref-02-bakan-ekrani-coop.png` bakan ekranının bağlayıcı referansıdır. Aynı koyu HUD
dili: paneller `rgba(14,18,25,.86)`, kenarlık `rgba(255,255,255,.09)`, köşe 14 px, zemin `#0B0E13`,
eylem `#F5B33C`, gerçek/tehlike `#F2564B`, rapor/dürüstlük `#3FCF77`, gizli hedef `#C48CFF`.
