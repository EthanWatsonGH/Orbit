mergeInto(LibraryManager.library, {
  CopyToClipboard: function (str) {
    const text = UTF8ToString(str);
    navigator.clipboard.writeText(text).catch(function (err) {
      console.error("Failed to copy: ", err);
    });
  },
  ReadFromClipboard: function () {
    navigator.clipboard.readText().then(function(text) {
      SendMessage("LevelEditor", "ReceiveClipboardText", text);
    }).catch(function(err) {
      console.error("Failed to read clipboard: ", err);
    });
  },
  ReadCodeClipboard: function (targetPtr, request) {
    const target = UTF8ToString(targetPtr);
    const pending = Module.orbitCodeClipboard || (Module.orbitCodeClipboard = {});
    const state = pending[target] || (pending[target] = {});
    state.read = request;
    function reply(value) {
      if (pending[target] !== state || state.read !== request) return;
      delete state.read;
      SendMessage(target, "ReceiveCodeClipboardRead", request + "\n" + value);
    }
    if (!navigator.clipboard || !navigator.clipboard.readText) {
      Promise.resolve().then(function() { reply("error"); });
      return;
    }
    try {
      navigator.clipboard.readText().then(function(text) { reply("ok:" + text); })
        .catch(function() { reply("error"); });
    } catch (error) { Promise.resolve().then(function() { reply("error"); }); }
  },
  WriteCodeClipboard: function (targetPtr, request, textPtr) {
    const target = UTF8ToString(targetPtr);
    const text = UTF8ToString(textPtr);
    const pending = Module.orbitCodeClipboard || (Module.orbitCodeClipboard = {});
    const state = pending[target] || (pending[target] = {});
    state.write = request;
    function reply(value) {
      if (pending[target] !== state || state.write !== request) return;
      delete state.write;
      SendMessage(target, "ReceiveCodeClipboardWrite", request + "\n" + value);
    }
    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      Promise.resolve().then(function() { reply("error"); });
      return;
    }
    try {
      navigator.clipboard.writeText(text).then(function() { reply("ok"); })
        .catch(function() { reply("error"); });
    } catch (error) { Promise.resolve().then(function() { reply("error"); }); }
  },
  CancelCodeClipboard: function (targetPtr) {
    if (Module.orbitCodeClipboard)
      delete Module.orbitCodeClipboard[UTF8ToString(targetPtr)];
  }
});
