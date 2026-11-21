// Print template designer (Phase 24): reads the controls into a template object, posts it to the
// preview frame (debounced), reorders sections, and sends the JSON when saving.
(function () {
  "use strict";
  var editor = document.getElementById("template-editor");
  if (!editor) return;

  var base = {};
  try { base = JSON.parse(editor.dataset.template || "{}"); } catch (e) { base = {}; }
  var previewForm = document.getElementById("tpl-preview-form");
  var previewJson = document.getElementById("tpl-preview-json");
  var saveForm = document.getElementById("tpl-save-form");
  var saveJson = document.getElementById("tpl-save-json");
  var state = document.getElementById("tpl-state");
  var list = document.getElementById("tpl-sections");
  var timer = null;
  var dirty = false;

  function collect() {
    var t = JSON.parse(JSON.stringify(base));
    editor.querySelectorAll("[data-bool]").forEach(function (el) { t[el.dataset.bool] = el.checked; });
    editor.querySelectorAll("[data-text]").forEach(function (el) { t[el.dataset.text] = el.value.trim() || null; });
    editor.querySelectorAll("[data-select]").forEach(function (el) { t[el.dataset.select] = el.value; });
    editor.querySelectorAll("[data-number]").forEach(function (el) {
      var v = parseFloat(el.value);
      if (!isNaN(v)) t[el.dataset.number] = v;
    });
    ["HiddenInfo", "HiddenColumns", "HiddenTotals"].forEach(function (name) {
      var boxes = editor.querySelectorAll('[data-hide-list="' + name + '"]');
      if (boxes.length === 0) return;
      t[name] = Array.prototype.filter.call(boxes, function (b) { return !b.checked; }).map(function (b) { return b.value; });
    });
    t.SectionOrder = Array.prototype.map.call(list.querySelectorAll("li"), function (li) { return li.dataset.section; });
    t.HiddenSections = Array.prototype.filter.call(editor.querySelectorAll("[data-section-visible]"), function (b) { return !b.checked; })
      .map(function (b) { return b.dataset.sectionVisible; });
    return t;
  }

  function preview() {
    previewJson.value = JSON.stringify(collect());
    previewForm.submit();
  }

  function changed() {
    if (!dirty) {
      dirty = true;
      if (state) state.textContent = state.dataset.dirty;
      editor.querySelector(".sp-savebar").classList.add("is-dirty");
    }
    window.clearTimeout(timer);
    timer = window.setTimeout(preview, 500);
  }

  editor.addEventListener("input", changed);
  editor.addEventListener("change", changed);

  list.addEventListener("click", function (e) {
    var btn = e.target.closest("[data-move]");
    if (!btn) return;
    var li = btn.closest("li");
    if (btn.dataset.move === "up" && li.previousElementSibling) list.insertBefore(li, li.previousElementSibling);
    if (btn.dataset.move === "down" && li.nextElementSibling) list.insertBefore(li.nextElementSibling, li);
    btn.focus();
    changed();
  });

  saveForm.addEventListener("submit", function () {
    saveJson.value = JSON.stringify(collect());
    dirty = false;
  });

  window.addEventListener("beforeunload", function (e) {
    if (dirty) { e.preventDefault(); e.returnValue = ""; }
  });

  // A4 preview: real page size (96 dpi), scaled to the column width; follows the Landscape switch.
  var box = document.getElementById("tpl-a4-box");
  function fitA4() {
    if (!box) return;
    var frame = document.getElementById("tpl-preview-frame");
    var landscapeBox = editor.querySelector('[data-bool="Landscape"]');
    var landscape = landscapeBox ? landscapeBox.checked : box.dataset.landscape === "true";
    var w = landscape ? 1123 : 794, h = landscape ? 794 : 1123;
    var scale = Math.min(1, box.clientWidth / w);
    frame.style.width = w + "px";
    frame.style.height = h + "px";
    frame.style.transform = "scale(" + scale + ")";
    box.style.height = Math.ceil(h * scale) + "px";
  }
  window.addEventListener("resize", fitA4);
  editor.addEventListener("change", fitA4);
  fitA4();

  preview();
})();
