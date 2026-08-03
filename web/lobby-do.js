// lobby-do.js — MEŞRUİYET lobisinin Durable Object adaptörü.
//
// Mantık burada YOK: her mesaj web/lobby-core.js'e gider, çıkanlar dağıtılır. Aynı çekirdeği
// web/dev-server.mjs yerel Node'da sürer — bu dosya yalnız Cloudflare API'sine tercümedir.
//
// COOP.md §7'nin hibernation düzeltmesi uygulanmıştır: FETİH'in server.accept()'i DO'yu
// bellekte tutar ve süre faturalar; sıra tabanlı oyunumuzda fazlar arası uzun boşluk var,
// o yüzden state.acceptWebSocket + webSocketMessage/webSocketClose kullanılır. Bellekteki
// Map'ler hibernation'da yaşamaz: oyuncu kimliği serializeAttachment ile soketin üstünde
// taşınır, soket listesi her seferinde state.getWebSockets()'ten okunur.
//
// Kalıcılık FETİH deseni: oda durumu değiştikçe storage.put; DO tahliye edilse bile oda
// son bilinen durumdan devam eder — host çökerse maç kaybolmaz.

import Core from './lobby-core.js';

export class Lobby {
  constructor(state) {
    this.state = state;
    this.room = null;
    // Yükleme bitmeden istek işlenmesin; oda storage'da varsa oradan doğar.
    state.blockConcurrencyWhile(async () => {
      const saved = await state.storage.get('room');
      if (saved) {
        this.room = saved;
        for (const p of this.room.players) p.connected = false;
      }
    });
  }

  persist() { try { this.state.storage.put('room', this.room); } catch (e) { /* yut */ } }

  armAlarm() {
    const at = Core.nextAlarmAt(this.room);
    try {
      if (at == null) this.state.storage.deleteAlarm();
      else this.state.storage.setAlarm(at);
    } catch (e) { /* yut */ }
  }

  dispatch(out) {
    const socks = this.state.getWebSockets();
    for (const { to, msg } of out) {
      const line = JSON.stringify(msg);
      for (const ws of socks) {
        let pid = null;
        try { pid = ws.deserializeAttachment(); } catch (e) { continue; }
        if (to === 'all' || to === pid) {
          try { ws.send(line); } catch (e) { /* kopuk soket */ }
        }
      }
    }
  }

  async fetch(req) {
    const url = new URL(req.url);
    const m = /\/lobby\/([A-Za-z0-9]{1,12})$/.exec(url.pathname);
    if (req.headers.get('Upgrade') !== 'websocket') return new Response('WS only', { status: 426 });

    if (!this.room) this.room = Core.newRoom(m ? m[1].toUpperCase() : '');
    else if (m && !this.room.code) this.room.code = m[1].toUpperCase();

    const pair = new WebSocketPair();
    const client = pair[0], server = pair[1];
    // Hibernation: runtime soketi sahiplenir; mesajlar aşağıdaki metotlara düşer.
    this.state.acceptWebSocket(server);
    return new Response(null, { status: 101, webSocket: client });
  }

  webSocketMessage(ws, data) {
    let msg;
    try { msg = JSON.parse(typeof data === 'string' ? data : new TextDecoder().decode(data)); }
    catch (e) { return; }

    let pid = null;
    try { pid = ws.deserializeAttachment(); } catch (e) { /* ilk mesaj */ }
    if (msg.t === 'hello') {
      pid = String(msg.pid || '').slice(0, 64);
      try { ws.serializeAttachment(pid); } catch (e) { /* yut */ }
    }
    if (!pid) return;

    if (!this.room) this.room = Core.newRoom('');
    const { out, changed } = Core.handleMessage(this.room, pid, msg, Date.now());
    this.dispatch(out);
    if (changed) this.persist();
    this.armAlarm();
  }

  webSocketClose(ws) {
    let pid = null;
    try { pid = ws.deserializeAttachment(); } catch (e) { return; }
    if (!pid || !this.room) return;
    const { out } = Core.handleDisconnect(this.room, pid, Date.now());
    this.dispatch(out);
    this.persist();
    this.armAlarm();
  }

  webSocketError(ws) { this.webSocketClose(ws); }

  async alarm() {
    if (!this.room) return;
    const { out, changed } = Core.onAlarm(this.room, Date.now());
    this.dispatch(out);
    if (changed) this.persist();
    this.armAlarm();
  }
}
