mergeInto(LibraryManager.library, {
  GrayExitPage: function () {
    if (typeof window === "undefined") {
      return;
    }

    if (window.history && window.history.length > 1) {
      window.history.back();
      return;
    }

    window.open("", "_self");
    window.close();
  }
});
