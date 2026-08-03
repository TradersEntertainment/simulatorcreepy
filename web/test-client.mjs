// test-client.mjs — senaryonun ikinci oyuncusu: Tarım koltuğuna oturur, BAKAN fazını
// bekler, yapılandırılmış (yalanlı) raporunu gönderir, sonra valinin RoleView kesitini
// bekler ve İÇERİĞİNİ DENETLER: kendi masasının satırları gelmiş mi, başka masanın sırrı
// sızmış mı. Güvenlik sınırının testi telin öbür ucundadır — sızıntı ancak burada görülür.
//
// Kullanım: node web/test-client.mjs ws://127.0.0.1:8492 TEST42 2 [raporGecikmesiMs]

const [, , base, code, seatArg, delayArg] = process.argv;
const seat = parseInt(seatArg || '2', 10);
const reportDelay = parseInt(delayArg || '0', 10);
const ws = new WebSocket(`${base}/lobby/${code}`);
const pid = 'client-' + seat;

// Tarım masasının satırları; RoleView kesiti tam bu anahtarları taşımalı, fazlasını asla.
const MY_LINES = ['tahil', 'ekmek', 'tampon'];
const FORBIDDEN = ['para', 'gelir', 'malzeme', 'depo', 'ordu', 'hosnutsuzluk', 'nufus'];

let reported = false;
const timeout = setTimeout(() => { console.log('zaman doldu'); process.exit(2); }, 90_000);

ws.addEventListener('open', () => {
  ws.send(JSON.stringify({ t: 'hello', pid, name: 'Bakan' + seat }));
  ws.send(JSON.stringify({ t: 'koltuk', n: seat }));
});

ws.addEventListener('message', (e) => {
  const msg = JSON.parse(e.data);

  if (msg.t === 'faz' && msg.name === 'bakan' && !reported) {
    reported = true;
    // The lie: the granary claimed at 900 against whatever the truth is.
    setTimeout(() => ws.send(JSON.stringify({
      t: 'rapor',
      data: { satirlar: [{ satir: 'tahil', deger: 900 }, { satir: 'tampon', deger: 9 }] },
    })), reportDelay);
  }

  if (msg.t === 'durum' && reported) {
    const d = msg.data || {};
    const keys = (d.satirlar || []).map((s) => s.satir);
    const mineOk = MY_LINES.every((k) => keys.includes(k));
    const leak = keys.find((k) => FORBIDDEN.includes(k));
    if (d.koltuk !== seat) { console.log('yanlış koltuğun kesiti geldi'); process.exit(3); }
    if (!mineOk) { console.log('kendi satırlarım eksik: ' + keys.join(',')); process.exit(4); }
    if (leak) { console.log('SIZINTI: ' + leak); process.exit(5); }
    console.log('rolview kesiti temiz: ' + keys.join(','));
    clearTimeout(timeout);
    ws.close();
    process.exit(0);
  }
});

ws.addEventListener('error', () => { console.log('bağlantı hatası'); process.exit(1); });
