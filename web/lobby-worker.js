// lobby-worker.js — MEŞRUİYET lobi Worker girişi (Cloudflare, üretim).
//
// /lobby/<KOD> WebSocket upgrade'ini o koda ait Durable Object örneğine yönlendirir:
// idFromName(KOD) → aynı koda bağlanan herkes aynı odaya düşer. FETİH'in worker.js deseni.
//
// Origin allowlist: Pages alan adları + yerel geliştirme. Masaüstü Unity istemcisi
// (ClientWebSocket) Origin başlığı göndermez — boş origin bilerek serbesttir; tarayacı
// istemciler (WebGL) ise listedeki bir origin'den gelmek zorundadır.

export { Lobby } from './lobby-do.js';

const ALLOWED = [
  /\.pages\.dev$/i,
  /^https?:\/\/localhost(:\d+)?$/i,
  /^https?:\/\/127\.0\.0\.1(:\d+)?$/i,
];

export default {
  async fetch(req, env) {
    const url = new URL(req.url);
    const m = /^\/lobby\/([A-Za-z0-9]{1,12})$/.exec(url.pathname);
    if (!m) return new Response('MEŞRUİYET lobi sunucusu.', { status: 200 });
    if (req.headers.get('Upgrade') !== 'websocket') return new Response('WebSocket only', { status: 426 });

    const origin = req.headers.get('Origin') || '';
    if (origin && !ALLOWED.some((re) => re.test(origin))) return new Response('forbidden', { status: 403 });

    const code = m[1].toUpperCase();
    const id = env.LOBBY.idFromName(code);
    return env.LOBBY.get(id).fetch(req);
  },
};
