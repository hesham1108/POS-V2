// Silent Windows print when possible; otherwise loads receipt in an off-screen iframe with ?autoprint=1.
// Buttons: data-print-url + optional data-invoice-id / data-print-format ("80mm"|"A4").
(() => {
  function antiforgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value
      || document.querySelector('meta[name="request-verification-token"]')?.content
      || "";
  }

  async function silentPrintSale(invoiceId, format) {
    const body = new URLSearchParams();
    body.set("id", String(invoiceId));
    if (format) body.set("format", format);
    const token = antiforgeryToken();
    if (token) body.set("__RequestVerificationToken", token);

    const res = await fetch("/Receipts/SilentPrintSale", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded;charset=UTF-8",
        "RequestVerificationToken": token
      },
      body
    });
    if (!res.ok) return null;
    return res.json();
  }

  function browserPrint(url) {
    if (!url) return;
    const href = url + (url.includes("?") ? "&" : "?") + "autoprint=1";
    const old = document.getElementById("print-frame");
    if (old) old.remove();

    const frame = document.createElement("iframe");
    frame.id = "print-frame";
    frame.title = "print";
    frame.setAttribute("aria-hidden", "true");
    frame.style.cssText =
      "position:fixed;left:-10000px;top:0;width:800px;height:1200px;border:0;opacity:0;pointer-events:none;";

    let printed = false;
    const tryPrint = () => {
      if (printed) return;
      printed = true;
      try {
        const win = frame.contentWindow;
        if (win) {
          win.focus();
          win.print();
        }
      } catch (_) { /* receipt page may auto-print */ }
    };

    frame.addEventListener("load", () => {
      setTimeout(tryPrint, 200);
      setTimeout(tryPrint, 800);
    });

    document.body.appendChild(frame);
    frame.src = href;
  }

  async function print(url, options = {}) {
    const invoiceId = options.invoiceId
      || (typeof url === "string" && /\/Receipts\/Sale(?:A4)?\/(\d+)/i.exec(url)?.[1]);
    const format = options.format
      || (typeof url === "string" && /SaleA4/i.test(url) ? "A4" : "80mm");

    if (invoiceId) {
      try {
        const result = await silentPrintSale(invoiceId, format);
        if (result?.printed) {
          return;
        }
      } catch (_) {
        // fall through to browser
      }
    }

    browserPrint(url);
  }

  window.StoresPrint = (url, options) => { print(url, options || {}); };

  document.addEventListener("click", (e) => {
    const button = e.target.closest("[data-print-url]");
    if (!button || button.disabled) return;
    e.preventDefault();
    print(button.dataset.printUrl, {
      invoiceId: button.dataset.invoiceId,
      format: button.dataset.printFormat
    });
  });

  // Paper size pickers (Phase 19): [data-format-picker] radios rewrite every [data-format-base] URL.
  function withFormat(base, format) {
    const u = new URL(base, window.location.origin);
    u.searchParams.set("format", format);
    return u.pathname + u.search;
  }

  function applyFormat(format) {
    document.querySelectorAll("[data-format-base]").forEach((el) => {
      const url = withFormat(el.dataset.formatBase, format);
      if (el.hasAttribute("data-print-url")) el.dataset.printUrl = url;
      if (el.hasAttribute("data-print-on-load")) el.dataset.printOnLoad = url;
      if (el.tagName === "A") el.setAttribute("href", url);
    });
  }

  document.querySelectorAll("[data-format-picker]").forEach((picker) => {
    const storageKey = "sp.paper." + picker.dataset.formatPicker;
    let saved = null;
    try { saved = window.localStorage.getItem(storageKey); } catch (_) { saved = null; }
    const initial = saved === "A4" || saved === "80mm" ? saved : (picker.dataset.formatDefault || "80mm");
    picker.querySelectorAll("input[type=radio]").forEach((r) => {
      r.checked = r.value === initial;
      r.addEventListener("change", () => {
        if (!r.checked) return;
        try { window.localStorage.setItem(storageKey, r.value); } catch (_) { /* private mode */ }
        applyFormat(r.value);
      });
    });
    applyFormat(initial);
  });

  const auto = document.querySelector("[data-print-on-load]");
  if (auto) window.addEventListener("load", () => print(auto.dataset.printOnLoad));
})();
