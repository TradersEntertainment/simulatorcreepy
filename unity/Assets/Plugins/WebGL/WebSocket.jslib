// The thin browser WebSocket wrapper COOP.md §7 calls for. No protocol knowledge here:
// strings out, strings in, a per-socket inbox array the C# side drains once a frame.
// States: 0 connecting · 1 open · 2 closed · 3 error.
mergeInto(LibraryManager.library, {

  MesNetConnect: function (urlPtr) {
    if (!window.mesNetSockets) { window.mesNetSockets = {}; window.mesNetNextId = 1; }
    var id = window.mesNetNextId++;
    var entry = { ws: null, inbox: [], state: 0, error: "" };
    try {
      var ws = new WebSocket(UTF8ToString(urlPtr));
      entry.ws = ws;
      ws.onopen = function () { entry.state = 1; };
      ws.onmessage = function (e) {
        if (typeof e.data === "string") entry.inbox.push(e.data);
      };
      ws.onerror = function () { entry.state = 3; entry.error = "websocket error"; };
      ws.onclose = function () { if (entry.state !== 3) entry.state = 2; };
    } catch (err) {
      entry.state = 3;
      entry.error = "" + err;
    }
    window.mesNetSockets[id] = entry;
    return id;
  },

  MesNetState: function (id) {
    var e = window.mesNetSockets && window.mesNetSockets[id];
    return e ? e.state : 3;
  },

  MesNetSend: function (id, msgPtr) {
    var e = window.mesNetSockets && window.mesNetSockets[id];
    if (e && e.ws && e.state === 1) e.ws.send(UTF8ToString(msgPtr));
  },

  // Returns a malloc'd UTF-8 string the caller must hand back to MesNetFree, or 0.
  MesNetPoll: function (id) {
    var e = window.mesNetSockets && window.mesNetSockets[id];
    if (!e || e.inbox.length === 0) return 0;
    return stringToNewUTF8(e.inbox.shift());
  },

  MesNetFree: function (ptr) { _free(ptr); },

  MesNetError: function (id) {
    var e = window.mesNetSockets && window.mesNetSockets[id];
    return stringToNewUTF8(e ? e.error : "no socket");
  },

  MesNetClose: function (id) {
    var e = window.mesNetSockets && window.mesNetSockets[id];
    if (e) {
      try { if (e.ws) e.ws.close(); } catch (err) {}
      delete window.mesNetSockets[id];
    }
  },
});
