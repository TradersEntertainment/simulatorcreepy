// lobby-core.js — MEŞRUİYET co-op lobisinin platform-bağımsız çekirdeği.
//
// COOP.md §7: FETİH'in üç katman deseni. Bu dosya ağ API'si bilmez — oda durumu, koltuklar,
// faz saati ve mesaj yönlendirmeden ibarettir. Aynı çekirdeği web/dev-server.mjs (yerel Node,
// Cloudflare'siz test) ve web/lobby-do.js (Durable Object adaptörü) sürer; mantık tek yerde
// yaşar ve deploy edilmeden test edilir.
//
// Yetki modeli FETİH'ten farklı ve bilerek böyle: oyun kuralı BURADA YOK. Vali'nin istemcisi
// GameState'i tutar ve turu çözer; sunucu aptal bir röledir artı bir son-tarih saati artı bir
// yedek. Tek kural-benzeri iş: BAKAN fazının süresini sunucu uygular — host sussa da faz biter.
//
// Koltuklar: 0 VALİ · 1 MALİYE · 2 TARIM · 3 GÜVENLİK · 4 İMAR · 5 HALK (köprüyle aynı sayı).

const SEATS = 6;
const MIN_PHASE_MS = 10_000;
const MAX_PHASE_MS = 300_000;

function newRoom(code) {
  return {
    code: code || '',
    rev: 0,
    phase: 'lobi',                 // lobi · bakan · vali · cozum
    phaseDeadline: null,           // ms epoch; yalnız bakan fazında dolu
    bakanSuresi: 90_000,           // host ayarlar (COOP §3: 90 sn varsayılan)
    players: [],                   // {pid, name, seat, connected, ready, submitted, lastSeen}
    snapshot: null,                // hostun kaydettiği opak oyun durumu (yeniden bağlanma/devir)
    channels: [],                  // açılmış özel kanallar: {a, b} koltuk çiftleri
  };
}

function find(room, pid) { return room.players.find((p) => p.pid === pid) || null; }
function inSeat(room, seat) { return room.players.find((p) => p.seat === seat) || null; }
function vali(room) { return inSeat(room, 0); }

function stateMsg(room) {
  return {
    t: 'durum-lobi',
    code: room.code,
    rev: room.rev,
    phase: room.phase,
    deadline: room.phaseDeadline,
    bakanSuresi: room.bakanSuresi,
    channels: room.channels.map((c) => ({ a: c.a, b: c.b })),   // varlık; içerik asla
    players: room.players.map((p) => ({
      pid: p.pid, name: p.name, seat: p.seat,
      connected: p.connected, ready: p.ready, submitted: p.submitted,
    })),
  };
}

function broadcast(room, out) { out.push({ to: 'all', msg: stateMsg(room) }); }

/// Bakan fazındaki bütün insan bakan koltukları raporunu verdiyse vali fazına geç.
function maybeAdvance(room, out) {
  if (room.phase !== 'bakan') return;
  const waiting = room.players.filter((p) => p.seat >= 1 && p.connected && !p.submitted);
  if (waiting.length > 0) return;
  room.phase = 'vali';
  room.phaseDeadline = null;
  out.push({ to: 'all', msg: { t: 'faz', name: 'vali', reason: 'raporlar-tamam' } });
  broadcast(room, out);
}

function handleMessage(room, pid, msg, now) {
  const out = [];
  let changed = false;

  switch (msg.t) {
    case 'hello': {
      let p = find(room, pid);
      if (!p) {
        p = {
          pid, name: String(msg.name || 'oyuncu').slice(0, 24),
          seat: null, connected: true, ready: false, submitted: false, lastSeen: now,
        };
        room.players.push(p);
      } else {
        // Yeniden bağlanma: koltuk durur, bot Unity tarafında çekilir. Maç asla durmaz.
        p.connected = true;
        p.lastSeen = now;
        if (msg.name) p.name = String(msg.name).slice(0, 24);
      }
      changed = true;
      // Yeni gelene hostun son anlık görüntüsü de gider — kaldığı yerden kurulsun.
      if (room.snapshot != null) out.push({ to: pid, msg: { t: 'anlik', data: room.snapshot } });
      broadcast(room, out);
      break;
    }

    case 'koltuk': {
      const p = find(room, pid);
      if (!p) break;
      const n = Number(msg.n);
      if (n === -1) { p.seat = null; changed = true; broadcast(room, out); break; }
      if (!Number.isInteger(n) || n < 0 || n >= SEATS) break;
      const holder = inSeat(room, n);
      if (holder && holder.pid !== pid) {
        out.push({ to: pid, msg: { t: 'hata', err: 'koltuk-dolu', n } });
        break;
      }
      p.seat = n;
      changed = true;
      broadcast(room, out);
      break;
    }

    case 'hazir': {
      const p = find(room, pid);
      if (!p) break;
      p.ready = !!msg.on;
      changed = true;
      broadcast(room, out);
      break;
    }

    case 'sure': {
      const p = find(room, pid);
      if (!p || p.seat !== 0) break;
      const ms = Math.max(MIN_PHASE_MS, Math.min(MAX_PHASE_MS, Number(msg.ms) || 90_000));
      room.bakanSuresi = ms;
      changed = true;
      broadcast(room, out);
      break;
    }

    case 'baslat': {
      const p = find(room, pid);
      if (!p || p.seat !== 0) { out.push({ to: pid, msg: { t: 'hata', err: 'vali-degilsin' } }); break; }
      if (room.phase !== 'lobi') break;
      startBakan(room, now, out);
      changed = true;
      break;
    }

    // Vali fazlar arasında döngüyü sürer: çözümden sonra yeni bakan fazı açar.
    case 'faz': {
      const p = find(room, pid);
      if (!p || p.seat !== 0) break;
      if (msg.name === 'bakan') startBakan(room, now, out);
      else if (msg.name === 'cozum' || msg.name === 'vali') {
        room.phase = msg.name;
        room.phaseDeadline = null;
        out.push({ to: 'all', msg: { t: 'faz', name: room.phase, reason: 'vali' } });
        broadcast(room, out);
      }
      changed = true;
      break;
    }

    // Bir bakanın raporu: içerik valiye gider, gönderildiği herkese görünür olur.
    // İçerik filtreleme (RoleView) Unity tarafında, mesaj kurulurken yapılır.
    case 'rapor': {
      const p = find(room, pid);
      if (!p || p.seat == null || p.seat < 1) break;
      p.submitted = true;
      changed = true;
      const v = vali(room);
      if (v) out.push({ to: v.pid, msg: { t: 'rapor', seat: p.seat, data: msg.data } });
      maybeAdvance(room, out);
      if (room.phase === 'bakan') broadcast(room, out);
      break;
    }

    case 'telgraf': {
      const p = find(room, pid);
      if (!p || p.seat == null) break;
      const v = vali(room);
      if (v) out.push({ to: v.pid, msg: { t: 'telgraf', seat: p.seat, data: msg.data } });
      break;
    }

    // Özel kanal: içerik yalnız hedef koltuğa; vali yalnız kanalın VARLIĞINI görür.
    case 'ozel': {
      const p = find(room, pid);
      if (!p || p.seat == null || p.seat < 1) break;
      const toSeat = Number(msg.to);
      if (!Number.isInteger(toSeat) || toSeat < 1 || toSeat >= SEATS || toSeat === p.seat) break;
      const target = inSeat(room, toSeat);
      if (target) out.push({ to: target.pid, msg: { t: 'ozel', from: p.seat, data: msg.data } });
      const a = Math.min(p.seat, toSeat), b = Math.max(p.seat, toSeat);
      if (!room.channels.some((c) => c.a === a && c.b === b)) {
        room.channels.push({ a, b });
        changed = true;
        broadcast(room, out);      // kanal sayısı herkese, içeriği kimseye
      }
      break;
    }

    // Vali fermanı ve tur çözümü: herkese. `seat` verilirse yalnız o koltuğa —
    // RoleView'in koltuk başına kestiği paketler böyle dağıtılır.
    case 'ferman':
    case 'durum': {
      const p = find(room, pid);
      if (!p || p.seat !== 0) break;
      if (Number.isInteger(msg.seat)) {
        const target = inSeat(room, msg.seat);
        if (target) out.push({ to: target.pid, msg: { t: msg.t, data: msg.data } });
      } else {
        out.push({ to: 'all', msg: { t: msg.t, data: msg.data } });
      }
      break;
    }

    // Hostun anlık görüntüsü: host çökse bile oda son bilinen durumdan devam eder.
    case 'anlik': {
      const p = find(room, pid);
      if (!p || p.seat !== 0) break;
      room.snapshot = msg.data;
      changed = true;
      break;
    }

    default:
      break;
  }

  room.rev++;
  return { out, changed };
}

function startBakan(room, now, out) {
  room.phase = 'bakan';
  room.phaseDeadline = now + room.bakanSuresi;
  for (const p of room.players) p.submitted = false;
  out.push({ to: 'all', msg: { t: 'faz', name: 'bakan', deadline: room.phaseDeadline } });
  broadcast(room, out);
}

function handleDisconnect(room, pid, now) {
  const out = [];
  const p = find(room, pid);
  if (p) {
    p.connected = false;
    p.lastSeen = now;
    broadcast(room, out);
    // Faz bekleyen tek kişi düşen oyuncuysa faz takılmasın: bot Unity tarafında
    // raporu doldurur ama sunucu da düşeni beklemeden ilerleyebilmeli.
    maybeAdvance(room, out);
  }
  const empty = room.players.every((q) => !q.connected);
  room.rev++;
  return { out, changed: true, empty };
}

/// Bir sonraki mutlak son tarih — yalnız bakan fazının saati vardır. FETİH deseni:
/// gerçek-zaman tick yok, tek alarm.
function nextAlarmAt(room) {
  return room.phase === 'bakan' ? room.phaseDeadline : null;
}

/// Saat doldu: sunucu fazı kapatır. Host susmuş olsa bile masa valiye döner.
function onAlarm(room, now) {
  const out = [];
  if (room.phase !== 'bakan' || room.phaseDeadline == null || now < room.phaseDeadline)
    return { out, changed: false };
  room.phase = 'vali';
  room.phaseDeadline = null;
  out.push({ to: 'all', msg: { t: 'faz', name: 'vali', reason: 'sure-doldu' } });
  broadcast(room, out);
  room.rev++;
  return { out, changed: true };
}

const Core = { newRoom, handleMessage, handleDisconnect, nextAlarmAt, onAlarm, stateMsg };
export default Core;
