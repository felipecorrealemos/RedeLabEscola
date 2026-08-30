mergeInto(LibraryManager.library, {
  RedeLabAudio_InstallUnlockHandlers: function (receiverPtr) {
    if (window.RedeLabAudioUnlockHandlersInstalled) return;
    window.RedeLabAudioUnlockHandlersInstalled = true;
    var receiver = UTF8ToString(receiverPtr);

    function resolveAudioContext() {
      if (typeof WEBAudio !== "undefined" && WEBAudio.audioContext) {
        return WEBAudio.audioContext;
      }
      if (typeof Module !== "undefined" && Module.audioContext) {
        return Module.audioContext;
      }
      return null;
    }

    function removeHandlers() {
      document.removeEventListener("pointerdown", resumeAudio, true);
      document.removeEventListener("touchstart", resumeAudio, true);
      document.removeEventListener("keydown", resumeAudio, true);
    }

    function resumeAudio() {
      var context = resolveAudioContext();
      if (!context) return;
      if (context.state === "running") {
        removeHandlers();
        if (typeof SendMessage === "function") SendMessage(receiver, "OnWebGLAudioUnlocked", "");
        return;
      }
      var result = context.resume();
      if (result && typeof result.then === "function") {
        result.then(function () {
          if (context.state === "running") {
            removeHandlers();
            if (typeof SendMessage === "function") SendMessage(receiver, "OnWebGLAudioUnlocked", "");
          }
        }).catch(function (error) {
          if (typeof console !== "undefined" && console.warn) {
            console.warn("[RedeLab Audio] Nao foi possivel retomar o AudioContext.", error);
          }
          // Mantem os listeners para tentar novamente no proximo gesto valido.
        });
      }
    }

    document.addEventListener("pointerdown", resumeAudio, true);
    document.addEventListener("touchstart", resumeAudio, true);
    document.addEventListener("keydown", resumeAudio, true);
  },

  RedeLabAudio_ResumeContext: function (receiverPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var context = null;
    if (typeof WEBAudio !== "undefined" && WEBAudio.audioContext) {
      context = WEBAudio.audioContext;
    } else if (typeof Module !== "undefined" && Module.audioContext) {
      context = Module.audioContext;
    }
    if (context && context.state !== "running") {
      var result = context.resume();
      if (result && typeof result.then === "function") {
        result.then(function () {
          if (typeof SendMessage === "function") SendMessage(receiver, "OnWebGLAudioUnlocked", "");
        }).catch(function (error) {
          if (typeof console !== "undefined" && console.warn) {
            console.warn("[RedeLab Audio] Nao foi possivel retomar o AudioContext.", error);
          }
        });
      }
    } else if (context && typeof SendMessage === "function") {
      SendMessage(receiver, "OnWebGLAudioUnlocked", "");
    }
  }
});
