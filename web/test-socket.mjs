// test-socket.mjs — dev sunucusunun tel üstünde testi: el sıkışma, çerçeveleme, yönlendirme.
// Sunucuyu kendisi başlatır, iki gerçek WebSocket istemcisi bağlar, kapatıp çıkar.
//
// Çalıştır: node web/test-socket.mjs

import { spawn } from 'node:child_process';
import { setTimeout as wait } from 'node:timers/promises';

const PORT = 8493;
const server = spawn(process.execPath, ['web/dev-server.mjs'], {
  env: { ...process.env, PORT: String(PORT) },
  stdio: 'ignore',
});

let pass = 0, fail = 0;
function check(ok, label) {
  if (ok) { pass++; console.log('  ✔ ' + label); }
  else { fail++; console.log('  ✘ ' + label); }
}

function client(pid) {
  const ws = new WebSocket(`ws://127.0.0.1:${PORT}/lobby/TEST1`);
  const inbox = [];
  ws.addEventListener('message', (e) => inbox.push(JSON.parse(e.data)));
  return { ws, inbox, pid,
    open: () => new Promise((r) => ws.addEventListener('open', r, { once: true })),
    send: (m) => ws.send(JSON.stringify(m)),
    last: (t) => [...inbox].reverse().find((m) => m.t === t) || null,
  };
}

try {
  await wait(700);                                   // sunucu dinlemeye geçsin

  const vali = client('v1');
  const bakan = client('b1');
  await vali.open();
  await bakan.open();

  vali.send({ t: 'hello', pid: 'v1', name: 'Vali' });
  bakan.send({ t: 'hello', pid: 'b1', name: 'Tarımcı' });
  await wait(300);
  check(vali.last('durum-lobi')?.players.length === 2, 'iki istemci aynı lobide');

  vali.send({ t: 'koltuk', n: 0 });
  bakan.send({ t: 'koltuk', n: 2 });
  await wait(300);
  const durum = bakan.last('durum-lobi');
  check(durum?.players.some((p) => p.pid === 'v1' && p.seat === 0), 'vali koltuğu telden işlendi');
  check(durum?.players.some((p) => p.pid === 'b1' && p.seat === 2), 'bakan koltuğu telden işlendi');

  vali.send({ t: 'baslat' });
  await wait(300);
  check(bakan.last('faz')?.name === 'bakan', 'faz geçişi her iki uca yayınlandı');

  bakan.send({ t: 'rapor', data: { tahil: 999 } });
  await wait(300);
  check(vali.last('rapor')?.data?.tahil === 999, 'rapor telden valiye ulaştı');
  check(vali.last('faz')?.reason === 'raporlar-tamam', 'tek bakanlı masada faz kendiliğinden ilerledi');

  vali.ws.close();
  bakan.ws.close();
  await wait(200);
} finally {
  server.kill();
}

console.log(`\n${pass} ✔ · ${fail} ✘`);
process.exit(fail === 0 ? 0 : 1);
