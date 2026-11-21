$(function () {
  // Shared jQuery helpers for StoresPOS.
  window.StoresPos = window.StoresPos || {};

  window.StoresPos.$ = $;

  window.StoresPos.on = function (selector, eventName, handler) {
    $(document).on(eventName, selector, handler);
  };
});

// Phase 20 — shared form behaviour for every <form class="sp-form">:
// unsaved-changes warning, no double submit, numeric keyboards for number fields.
(function () {
  "use strict";
  var leaveText = document.documentElement.getAttribute("data-leave-text") || "";

  document.querySelectorAll("form.sp-form").forEach(function (form) {
    var dirty = false;
    var submitting = false;
    var state = form.querySelector(".sp-form__state");

    function mark(value) {
      dirty = value;
      form.classList.toggle("is-dirty", value);
      if (state) state.textContent = value ? (state.dataset.dirty || "") : (state.dataset.clean || "");
    }

    form.querySelectorAll('input[type="number"]:not([inputmode])').forEach(function (el) {
      var step = el.getAttribute("step");
      el.setAttribute("inputmode", step && step !== "1" ? "decimal" : "numeric");
    });

    form.addEventListener("input", function () { mark(true); });
    form.addEventListener("change", function () { mark(true); });
    form.addEventListener("reset", function () { setTimeout(function () { mark(false); }, 0); });

    form.addEventListener("submit", function (e) {
      if (submitting) { e.preventDefault(); return; }
      // Let jQuery validation stop invalid forms without locking the buttons.
      if (window.jQuery && jQuery(form).valid && !jQuery(form).valid()) return;
      submitting = true;
      // Disable after the browser has collected the form data, so the clicked button's name/value is still sent.
      setTimeout(function () {
        form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (b) {
          b.disabled = true;
          b.setAttribute("aria-busy", "true");
        });
      }, 0);
      // A failed submit (network) should not lock the page forever.
      setTimeout(function () {
        submitting = false;
        form.querySelectorAll('[aria-busy="true"]').forEach(function (b) { b.disabled = false; b.removeAttribute("aria-busy"); });
      }, 15000);
    });

    window.addEventListener("beforeunload", function (e) {
      if (dirty && !submitting && !form.hasAttribute("data-no-leave-warning")) {
        e.preventDefault();
        e.returnValue = leaveText;
        return leaveText;
      }
    });
  });
})();

// Phase 20 — required marks: label of every required field in an .sp-form gets a red asterisk.
(function () {
  document.querySelectorAll("form.sp-form [data-val-required], form.sp-form [required]").forEach(function (el) {
    if (!el.id) return;
    var label = document.querySelector('label[for="' + el.id + '"]');
    if (label && !label.classList.contains("form-check-label")) label.classList.add("required");
  });
})();
