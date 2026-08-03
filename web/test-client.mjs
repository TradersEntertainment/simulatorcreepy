// test-client.mjs — senaryonun ikinci oyuncusu: bir bakan koltuğuna oturur, BAKAN fazını
// bekler, raporunu gönderir ve VALİ fazını görünce çıkar. Unity'deki vali ile aynı odada
// gerçek bir ağ tur atışı yapmak için loop.ps1'in coop senaryosu tarafından çalıştırılır.
//
// Kullanım: node web/test-client.mjs ws://127.0.0.1:8492 TEST42 2 [raporGecikmesiMs]
// Gecikme, BAKAN fazının dışarıdan gözlemlenebilir kalması için: gerçek insan da anında
// göndermez.

const [, , base, code, seatArg, delayArg] = process.argv;
const seat = parseInt(seatArg || '2', 10);
const reportDelay = parseInt(delayArg || '0', 10);
const ws = new WebSocket(`${base}/lobby/${code}`);
const pid = 'client-' + seat;

let reported = false;
const timeout = setTimeout(() => { console.log('zaman doldu'); process.exit(2); }, 60_000);

ws.addEventListener('open', () => {
  ws.send(JSON.stringify({ t: 'hello', pid, name: 'Bakan' + seat }));
  ws.send(JSON.stringify({ t: 'koltuk', n: seat }));
});

ws.addEventListener('message', (e) => {
  const msg = JSON.parse(e.data);
  if (msg.t === 'faz' && msg.name === 'bakan' && !reported) {
    reported = true;
    setTimeout(() => ws.send(JSON.stringify({ t: 'rapor', data: { tahil: 999 } })), reportDelay);
  }
  if (msg.t === 'faz' && msg.name === 'vali' && reported) {
    console.log('rapor verildi, faz vali — tamam');
    clearTimeout(timeout);
    ws.close();
    process.exit(0);
  }
});

ws.addEventListener('error', () => { console.log('bağlantı hatası'); process.exit(1); });
