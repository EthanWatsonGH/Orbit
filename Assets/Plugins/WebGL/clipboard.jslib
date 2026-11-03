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
  ReadPrefabClipboard: function (targetPtr, requestId) {
    const target = UTF8ToString(targetPtr);
    function reply(value) { SendMessage(target, "ReceivePrefabClipboard", requestId + "\n" + value); }
    if (!navigator.clipboard || !navigator.clipboard.readText) {
      reply("error");
      return;
    }
    navigator.clipboard.readText().then(function(text) {
      reply("ok:" + text);
    }).catch(function() { reply("error"); });
  },
  CopyPrefabClipboard: function (targetPtr, requestId, codePtr) {
    const target = UTF8ToString(targetPtr);
    const code = UTF8ToString(codePtr);
    function reply(value) { SendMessage(target, "ReceivePrefabCopyResult", requestId + "\n" + value); }
    if (!navigator.clipboard || !navigator.clipboard.writeText) {
      reply("error");
      return;
    }
    navigator.clipboard.writeText(code).then(function() {
      reply("ok");
    }).catch(function() { reply("error"); });
  }
});
