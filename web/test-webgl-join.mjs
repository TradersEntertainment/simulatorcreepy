// test-webgl-join.mjs — dilim 6'nın tarayıcı kapısı: WebGL istemcisinin ?lobi=KOD linkiyle
// gerçekten lobiye düştüğünü telin ÖBÜR ucundan doğrular. Vali olarak bağlanır, koltuk 0'ı
// alır ve verilen isimde ikinci bir oyuncunun (tarayıcıdaki Unity) görünmesini bekler.
//
// Kullanım: node web/test-webgl-join.mjs ws://127.0.0.1:8092 TEST42 web-bakan [saniye]

const [, , base, code, expectName, secArg] = process.argv;
const patience = (parseInt(secArg || '120', 10)) * 1000;
const ws = new WebSocket(`${base}/lobby/${code}`);

const timeout = setTimeout(() => {
  console.log(`zaman doldu: '${expectName}' lobiye hiç düşmedi`);
  process.exit(2);
}, patience);

ws.addEventListener('open', () => {
  ws.send(JSON.stringify({ t: 'hello', pid: 'gozcu-vali', name: 'Vali' }));
  ws.send(JSON.stringify({ t: 'koltuk', n: 0 }));
});

ws.addEventListener('message', (e) => {
  const msg = JSON.parse(e.data);
  if (msg.t !== 'durum-lobi') return;
  const players = msg.players || [];
  const guest = players.find((p) => p.name === expectName && p.connected);
  if (guest) {
    console.log(`tarayıcı oyuncusu lobide: ${guest.name} (pid ${guest.pid}, koltuk ${guest.seat})`);
    clearTimeout(timeout);
    ws.close();
    process.exit(0);
  }
  console.log(`lobi: ${players.length} oyuncu — bekleniyor...`);
});

ws.addEventListener('error', () => { console.log('gözcü bağlanamadı'); process.exit(1); });
