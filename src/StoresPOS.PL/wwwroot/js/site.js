$(function () {
  // Shared jQuery helpers for StoresPOS.
  window.StoresPos = window.StoresPos || {};

  window.StoresPos.$ = $;

  window.StoresPos.on = function (selector, eventName, handler) {
    $(document).on(eventName, selector, handler);
  };
});
