mergeInto(LibraryManager.library, {
  RedeLabBrowser_TryClose: function (receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    window.close();
    window.setTimeout(function () {
      if (!window.closed) {
        SendMessage(receiver, "OnWebGLWindowCloseBlocked", "");
      }
    }, 150);
  }
});
