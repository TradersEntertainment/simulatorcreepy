// dev-server.mjs — MEŞRUİYET lobisinin yerel Node sunucusu (yalnız geliştirme/test).
//
// web/lobby-core.js'i AYNEN sürer → Durable Object adaptörüyle aynı mantık, Cloudflare'e hiç
// dokunmadan altı koltuklu lobi testi. WS el sıkışması + çerçeveleme FETİH'in dev sunucusundan
// alınan desenle elle uygulanır (RFC 6455) — bağımlılık YOK.
//
// Çalıştır:  node web/dev-server.mjs             (varsayılan port 8092)
//            PORT=9100 node web/dev-server.mjs
// Yol:       ws://127.0.0.1:8092/lobby/<KOD>

import http from 'node:http';
import crypto from 'node:crypto';
import Core from './lobby-core.js';

const PORT = parseInt(process.env.PORT || '8092', 10);
const WS_GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';

function accept(key) {
  return crypto.createHash('sha1').update(key + WS_GUID).digest('base64');
}
function encodeText(str) {
  const payload = Buffer.from(str, 'utf8');
  const len = payload.length;
  let header;
  if (len < 126) header = Buffer.from([0x81, len]);
  else if (len < 65536) { header = Buffer.alloc(4); header[0] = 0x81; header[1] = 126; header.writeUInt16BE(len, 2); }
  else { header = Buffer.alloc(10); header[0] = 0x81; header[1] = 127; header.writeUInt32BE(0, 2); header.writeUInt32BE(len, 6); }
  return Buffer.concat([header, payload]);
}

class WSConn {
  constructor(socket) {
    this.socket = socket;
    this.buf = Buffer.alloc(0);
    this.onmessage = null; this.onclose = null;
    this.alive = true;
    socket.on('data', (d) => this._feed(d));
    socket.on('close', () => this._die());
    socket.on('error', () => this._die());
  }
  _feed(d) {
    this.buf = Buffer.concat([this.buf, d]);
    for (;;) {
      if (this.buf.length < 2) return;
      const b0 = this.buf[0], b1 = this.buf[1];
      const opcode = b0 & 0x0f;
      const masked = (b1 & 0x80) !== 0;
      let len = b1 & 0x7f, off = 2;
      if (len === 126) { if (this.buf.length < 4) return; len = this.buf.readUInt16BE(2); off = 4; }
      else if (len === 127) { if (this.buf.length < 10) return; len = Number(this.buf.readBigUInt64BE(2)); off = 10; }
      let mask = null;
      if (masked) { if (this.buf.length < off + 4) return; mask = this.buf.slice(off, off + 4); off += 4; }
      if (this.buf.length < off + len) return;
      let payload = this.buf.slice(off, off + len);
      if (masked) { const u = Buffer.alloc(len); for (let i = 0; i < len; i++) u[i] = payload[i] ^ mask[i & 3]; payload = u; }
      this.buf = this.buf.slice(off + len);
      if (opcode === 0x8) { this._die(); return; }
      else if (opcode === 0x9) this.socket.write(Buffer.from([0x8a, 0x00]));
      else if (opcode === 0x1 || opcode === 0x0) {
        if (this.onmessage) { try { this.onmessage(payload.toString('utf8')); } catch (e) { /* yut */ } }
      }
    }
  }
  send(str) { if (this.alive) { try { this.socket.write(encodeText(str)); } catch (e) { /* yut */ } } }
  _die() { if (!this.alive) return; this.alive = false; if (this.onclose) this.onclose(); }
}

const rooms = new Map();   // kod → { room, conns:Map(pid→WSConn), alarm }
function getRoom(code) {
  let r = rooms.get(code);
  if (!r) { r = { room: Core.newRoom(code), conns: new Map(), alarm: null }; rooms.set(code, r); }
  return r;
}
function dispatch(entry, out) {
  for (const { to, msg } of out) {
    const line = JSON.stringify(msg);
    if (to === 'all') { for (const c of entry.conns.values()) c.send(line); }
    else { const c = entry.conns.get(to); if (c) c.send(line); }
  }
}
function schedule(entry) {
  if (entry.alarm) { clearTimeout(entry.alarm); entry.alarm = null; }
  const at = Core.nextAlarmAt(entry.room);
  if (at == null) return;
  entry.alarm = setTimeout(() => {
    entry.alarm = null;
    const { out } = Core.onAlarm(entry.room, Date.now());
    if (out.length) dispatch(entry, out);
    schedule(entry);
  }, Math.max(0, at - Date.now()));
}

const server = http.createServer((req, res) => { res.writeHead(426); res.end('WebSocket only'); });

server.on('upgrade', (req, socket) => {
  const m = /^\/lobby\/([A-Za-z0-9]{1,12})/.exec(req.url || '');
  const key = req.headers['sec-websocket-key'];
  if (!m || !key) { socket.end('HTTP/1.1 400 Bad Request\r\n\r\n'); return; }
  const code = m[1].toUpperCase();
  socket.write(
    'HTTP/1.1 101 Switching Protocols\r\n' +
    'Upgrade: websocket\r\nConnection: Upgrade\r\n' +
    'Sec-WebSocket-Accept: ' + accept(key) + '\r\n\r\n'
  );
  const conn = new WSConn(socket);
  const entry = getRoom(code);
  let boundId = null;

  conn.onmessage = (line) => {
    let msg; try { msg = JSON.parse(line); } catch (e) { return; }
    const pid = (msg.t === 'hello') ? String(msg.pid || '').slice(0, 64) : boundId;
    if (!pid) return;
    if (msg.t === 'hello') { boundId = pid; entry.conns.set(pid, conn); }
    const { out } = Core.handleMessage(entry.room, pid, msg, Date.now());
    dispatch(entry, out);
    schedule(entry);
    console.log(`[${code}] ${msg.t} ← ${pid} (oyuncu ${entry.room.players.length}, faz ${entry.room.phase})`);
  };
  conn.onclose = () => {
    if (!boundId) return;
    if (entry.conns.get(boundId) === conn) entry.conns.delete(boundId);
    const { out, empty } = Core.handleDisconnect(entry.room, boundId, Date.now());
    dispatch(entry, out);
    schedule(entry);
    if (empty && entry.conns.size === 0) { if (entry.alarm) clearTimeout(entry.alarm); rooms.delete(code); }
  };
});

server.listen(PORT, () => {
  console.log(`MEŞRUİYET lobi dev WS — ws://127.0.0.1:${PORT}/lobby/<KOD>`);
});
