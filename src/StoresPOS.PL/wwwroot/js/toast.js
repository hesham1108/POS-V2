// StoresPOS — shared toast notifications (top-centre, stacked, above modals).
// Usage: window.StoresToast(text, kind)  kind: "success" | "warning" | "error" | "info" (also "ok" = success).
(() => {
  const ICONS = {
    success: '<circle cx="12" cy="12" r="9"/><path d="m8 12.5 2.7 2.7L16.5 9.5"/>',
    warning: '<path d="M12 3.5 2.5 20h19z"/><path d="M12 10v4.5"/><path d="M12 17.5h.01"/>',
    error: '<circle cx="12" cy="12" r="9"/><path d="m9 9 6 6"/><path d="m15 9-6 6"/>',
    info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v5.5"/><path d="M12 7.5h.01"/>'
  };
  const DURATION = { success: 4000, info: 4000, warning: 6500, error: 6500 };

  function container() {
    let box = document.getElementById("sp-toasts");
    if (!box) {
      box = document.createElement("div");
      box.id = "sp-toasts";
      box.className = "sp-toasts";
      box.setAttribute("aria-live", "polite");
      box.setAttribute("aria-relevant", "additions");
      document.body.appendChild(box);
    }
    return box;
  }

  function closeLabel() {
    return (document.documentElement.lang || "").startsWith("ar") ? "إغلاق" : "Close";
  }

  function dismiss(el) {
    if (!el || el.classList.contains("is-leaving")) return;
    el.classList.add("is-leaving");
    const done = () => el.remove();
    el.addEventListener("animationend", done, { once: true });
    setTimeout(done, 400); // reduced motion / no animation
  }

  function show(text, kind) {
    if (!text) return null;
    const type = kind === "ok" ? "success" : (ICONS[kind] ? kind : "info");
    const box = container();

    const el = document.createElement("div");
    el.className = "sp-toast sp-toast--" + type;
    el.setAttribute("role", type === "error" || type === "warning" ? "alert" : "status");

    const icon = document.createElement("span");
    icon.className = "sp-toast__icon";
    icon.innerHTML = '<svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + ICONS[type] + "</svg>";

    const body = document.createElement("div");
    body.className = "sp-toast__text";
    body.textContent = text;

    const close = document.createElement("button");
    close.type = "button";
    close.className = "sp-toast__close";
    close.setAttribute("aria-label", closeLabel());
    close.innerHTML = '<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12"/><path d="M18 6 6 18"/></svg>';
    close.addEventListener("click", () => dismiss(el));

    const bar = document.createElement("span");
    bar.className = "sp-toast__bar";
    bar.style.animationDuration = DURATION[type] + "ms";

    el.append(icon, body, close, bar);
    box.appendChild(el);

    // Keep at most 4 on screen.
    const all = box.querySelectorAll(".sp-toast:not(.is-leaving)");
    if (all.length > 4) dismiss(all[0]);

    let timer = setTimeout(() => dismiss(el), DURATION[type]);
    el.addEventListener("mouseenter", () => { clearTimeout(timer); el.classList.add("is-paused"); });
    el.addEventListener("mouseleave", () => { el.classList.remove("is-paused"); timer = setTimeout(() => dismiss(el), 1800); });
    return el;
  }

  window.StoresToast = show;
})();
