// Settings → Appearance: theme cards and colour palettes with live preview (nothing is saved until the form is).
(function () {
  "use strict";
  var root = document.documentElement;
  var HEX = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i;

  function full(hex) {
    hex = hex.replace("#", "");
    if (hex.length === 3) hex = hex.split("").map(function (c) { return c + c; }).join("");
    return "#" + hex.toLowerCase();
  }
  function luminance(hex) {
    var n = parseInt(full(hex).slice(1), 16);
    return (0.299 * ((n >> 16) & 255) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255)) / 255;
  }

  function previewPrimary(hex) {
    var s = root.style;
    if (!hex) {
      ["--sp-primary", "--sp-primary-hover", "--sp-primary-soft", "--sp-primary-border", "--sp-focus"].forEach(function (k) { s.removeProperty(k); });
      return;
    }
    s.setProperty("--sp-primary", hex);
    s.setProperty("--sp-focus", hex);
    s.setProperty("--sp-primary-hover", "color-mix(in srgb," + hex + " 85%,#000)");
    s.setProperty("--sp-primary-soft", "color-mix(in srgb," + hex + " 12%,transparent)");
    s.setProperty("--sp-primary-border", "color-mix(in srgb," + hex + " 40%,transparent)");
  }
  function previewSidebar(hex) {
    var s = root.style;
    var keys = ["--sp-sidebar", "--sp-sidebar-text", "--sp-sidebar-strong", "--sp-sidebar-muted", "--sp-sidebar-hover", "--sp-sidebar-border"];
    if (!hex) { keys.forEach(function (k) { s.removeProperty(k); }); return; }
    s.setProperty("--sp-sidebar", hex);
    if (luminance(hex) < 0.5) {
      s.setProperty("--sp-sidebar-text", "#d5d9e0");
      s.setProperty("--sp-sidebar-strong", "#ffffff");
      s.setProperty("--sp-sidebar-muted", "#9aa0a6");
      s.setProperty("--sp-sidebar-hover", "rgba(255,255,255,0.08)");
      s.setProperty("--sp-sidebar-border", "rgba(255,255,255,0.12)");
    } else {
      keys.slice(1).forEach(function (k) { s.removeProperty(k); });
    }
  }

  document.querySelectorAll("[data-theme-choice]").forEach(function (r) {
    r.addEventListener("change", function () { if (r.checked) root.setAttribute("data-theme", r.value); });
  });

  document.querySelectorAll("[data-color-field]").forEach(function (field) {
    var text = field.querySelector("[data-color-text]");
    var picker = field.querySelector("[data-color-picker]");
    var swatches = field.querySelectorAll("[data-swatch]");
    var isSidebar = field.getAttribute("data-target") === "sidebar";

    function apply(hex, fromText, silent) {
      var ok = !hex || HEX.test(hex);
      text.classList.toggle("is-invalid", !ok);
      if (!ok) return;
      var value = hex ? full(hex) : "";
      if (!fromText) text.value = value;
      picker.value = value || field.getAttribute("data-fallback");
      swatches.forEach(function (b) { b.classList.toggle("is-on", !!value && b.getAttribute("data-swatch").toLowerCase() === value); });
      (isSidebar ? previewSidebar : previewPrimary)(value);
      if (!silent) text.dispatchEvent(new Event("change", { bubbles: true }));
    }

    swatches.forEach(function (b) { b.addEventListener("click", function () { apply(b.getAttribute("data-swatch")); }); });
    picker.addEventListener("input", function () { apply(picker.value); });
    text.addEventListener("input", function () { apply(text.value.trim(), true); });
    field.querySelector("[data-color-reset]").addEventListener("click", function () { apply(""); });
    apply(text.value.trim(), true, true);
  });
})();
