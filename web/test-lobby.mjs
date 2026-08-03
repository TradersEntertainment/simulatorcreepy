// test-lobby.mjs — lobi çekirdeğinin saf testi. Soket yok, sunucu yok, Cloudflare yok:
// üç katman ayrımının bütün amacı bu dosyanın var olabilmesi. Altı koltuk, faz saati,
// yönlendirme ve yeniden bağlanma, hepsi bellekte sürülür ve çıkış kodu bunu söyler.
//
// Çalıştır: node web/test-lobby.mjs

import Core from './lobby-core.js';

let pass = 0, fail = 0;
function check(ok, label) {
  if (ok) { pass++; console.log('  ✔ ' + label); }
  else { fail++; console.log('  ✘ ' + label); }
}
function sent(out, to, t) {
  return out.some((o) => o.to === to && o.msg.t === t);
}

const now = 1_000_000;
const room = Core.newRoom('K7M2X');

// ---- altı oyuncu katılır, koltuklar dağılır
const pids = ['vali1', 'mal1', 'tar1', 'guv1', 'imr1', 'hlk1'];
for (const pid of pids) Core.handleMessage(room, pid, { t: 'hello', pid, name: pid }, now);
check(room.players.length === 6, 'altı oyuncu katıldı');

for (let i = 0; i < 6; i++) Core.handleMessage(room, pids[i], { t: 'koltuk', n: i }, now);
check(room.players.every((p) => p.seat != null), 'altı koltuğun altısı da dolu');

// ---- dolu koltuk kapılamaz
const dolu = Core.handleMessage(room, 'mal1', { t: 'koltuk', n: 0 }, now);
check(sent(dolu.out, 'mal1', 'hata'), 'dolu koltuk reddediliyor');
check(room.players.find((p) => p.pid === 'mal1').seat === 1, 'reddedilen yerinde kalıyor');

// ---- yalnız vali başlatır
const erken = Core.handleMessage(room, 'tar1', { t: 'baslat' }, now);
check(sent(erken.out, 'tar1', 'hata'), 'vali olmayan başlatamıyor');
check(room.phase === 'lobi', 'faz hâlâ lobi');

const basla = Core.handleMessage(room, 'vali1', { t: 'baslat' }, now);
check(room.phase === 'bakan', 'vali başlatınca BAKAN fazı açılıyor');
check(room.phaseDeadline === now + 90_000, 'faz saati 90 saniye');
check(Core.nextAlarmAt(room) === room.phaseDeadline, 'alarm son tarihe kurulu');

// ---- rapor yalnız valiye gider
const r1 = Core.handleMessage(room, 'tar1', { t: 'rapor', data: { tahil: 480 } }, now + 5_000);
check(sent(r1.out, 'vali1', 'rapor'), 'rapor valiye ulaşıyor');
check(!r1.out.some((o) => o.to === 'mal1' && o.msg.t === 'rapor'), 'rapor başka bakana gitmiyor');

// ---- özel kanal: içerik hedefe, varlık herkese
const oz = Core.handleMessage(room, 'guv1', { t: 'ozel', to: 4, data: 'depoyu topla' }, now + 6_000);
check(sent(oz.out, 'imr1', 'ozel'), 'özel mesaj hedef koltuğa gidiyor');
check(!oz.out.some((o) => o.to === 'vali1' && o.msg.t === 'ozel'), 'vali içeriği görmüyor');
check(room.channels.length === 1, 'kanalın varlığı kayıtlı');

// ---- süre dolunca sunucu fazı kapatır (host sussa bile)
const erkenAlarm = Core.onAlarm(room, now + 50_000);
check(erkenAlarm.changed === false, 'saat dolmadan alarm iş yapmıyor');
const alarm = Core.onAlarm(room, now + 90_001);
check(room.phase === 'vali', 'süre dolunca faz VALİ');
check(alarm.out.some((o) => o.msg.t === 'faz' && o.msg.reason === 'sure-doldu'), 'faz mesajı sebep bildiriyor');

// ---- yeni tur: herkes raporlayınca faz kendiliğinden ilerler
Core.handleMessage(room, 'vali1', { t: 'faz', name: 'bakan' }, now + 100_000);
check(room.phase === 'bakan', 'vali yeni bakan fazı açabiliyor');
check(room.players.every((p) => !p.submitted), 'yeni fazda raporlar sıfır');
for (const pid of ['mal1', 'tar1', 'guv1', 'imr1'])
  Core.handleMessage(room, pid, { t: 'rapor', data: {} }, now + 101_000);
check(room.phase === 'bakan', 'son rapor gelmeden faz beklemede');
const son = Core.handleMessage(room, 'hlk1', { t: 'rapor', data: {} }, now + 102_000);
check(room.phase === 'vali', 'beş rapor tamamlanınca faz kendiliğinden VALİ');
check(son.out.some((o) => o.msg.t === 'faz' && o.msg.reason === 'raporlar-tamam'), 'geçiş herkese duyuruluyor');

// ---- kopan oyuncu koltuğunu kaybetmez; oda boşalınca bunu söyler
Core.handleDisconnect(room, 'tar1', now + 110_000);
check(room.players.find((p) => p.pid === 'tar1').seat === 2, 'kopan oyuncunun koltuğu duruyor');
Core.handleMessage(room, 'tar1', { t: 'hello', pid: 'tar1', name: 'tar1' }, now + 120_000);
const tar = room.players.find((p) => p.pid === 'tar1');
check(tar.connected && tar.seat === 2, 'yeniden bağlanan aynı koltuğa oturuyor');

// ---- kopan raporcu fazı kilitleyemez
Core.handleMessage(room, 'vali1', { t: 'faz', name: 'bakan' }, now + 130_000);
for (const pid of ['mal1', 'guv1', 'imr1', 'hlk1'])
  Core.handleMessage(room, pid, { t: 'rapor', data: {} }, now + 131_000);
check(room.phase === 'bakan', 'düşmemiş raporcu beklenirken faz açık');
Core.handleDisconnect(room, 'tar1', now + 132_000);
check(room.phase === 'vali', 'raporu beklenen oyuncu düşünce faz ilerliyor');

// ---- anlık görüntü yeni gelene ulaşır
Core.handleMessage(room, 'vali1', { t: 'anlik', data: { tur: 7 } }, now + 140_000);
const yeni = Core.handleMessage(room, 'izleyici', { t: 'hello', pid: 'izleyici' }, now + 141_000);
check(sent(yeni.out, 'izleyici', 'anlik'), 'yeni gelen hostun son anlık görüntüsünü alıyor');

console.log(`\n${pass} ✔ · ${fail} ✘`);
process.exit(fail === 0 ? 0 : 1);
