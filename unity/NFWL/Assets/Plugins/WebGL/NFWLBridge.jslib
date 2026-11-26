// Unity WebGL <-> NFWL web page bridge (web/src/lib/bridge/web-bridge.ts installs window.NFWLBridge).
// Strings returned to C# are allocated with _malloc as in the Unity manual; Unity frees them.
mergeInto(LibraryManager.library, {
  NFWL_GetMatchConfig: function () {
    var s = "";
    try { if (window.NFWLBridge && window.NFWLBridge.getConfigJson) s = window.NFWLBridge.getConfigJson() || ""; } catch (e) {}
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },
  NFWL_GetTeams: function () {
    var s = "";
    try { if (window.NFWLBridge && window.NFWLBridge.getTeamsJson) s = window.NFWLBridge.getTeamsJson() || ""; } catch (e) {}
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },
  NFWL_Post: function (ptr) {
    var s = UTF8ToString(ptr);
    try { if (window.NFWLBridge && window.NFWLBridge.receive) window.NFWLBridge.receive(s); } catch (e) { console.error("[NFWLBridge]", e); }
  }
});
