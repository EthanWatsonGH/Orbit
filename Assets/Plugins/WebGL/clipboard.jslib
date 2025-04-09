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
  }
});