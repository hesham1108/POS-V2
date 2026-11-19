// Quick cash-out modal (journey E). Any element with [data-cashout] opens it.
// On success: dispatches "storespos:cashout" with the server JSON ({ message, warning, expectedCash }).
// Pages that do not handle the event reload so totals refresh.
(() => {
  const modalEl = document.getElementById("cashout-modal");
  if (!modalEl) return;

  const form = document.getElementById("cashout-form");
  const amount = document.getElementById("cashout-amount");
  const reason = document.getElementById("cashout-reason");
  const error = document.getElementById("cashout-error");
  const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
  const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
  let busy = false;

  function open() {
    form.reset();
    error.textContent = "";
    modal.show();
  }

  window.StoresCashOut = open;
  modalEl.addEventListener("shown.bs.modal", () => amount.focus());

  document.addEventListener("click", (e) => {
    const trigger = e.target.closest("[data-cashout]");
    if (!trigger) return;
    e.preventDefault();
    open();
  });

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (busy) return;
    error.textContent = "";

    const value = parseFloat(amount.value.replace(",", "."));
    if (!(value > 0)) { error.textContent = form.dataset.msgAmount; amount.focus(); return; }
    if (!reason.value.trim()) { error.textContent = form.dataset.msgReason; reason.focus(); return; }

    busy = true;
    try {
      const response = await fetch(form.dataset.url, {
        method: "POST",
        credentials: "same-origin",
        headers: { "RequestVerificationToken": token, "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams({ amount: String(value), reason: reason.value.trim() })
      });
      const text = await response.text();
      if (!response.ok) { error.textContent = text || form.dataset.msgError; return; }

      const result = JSON.parse(text);
      modal.hide();
      const handled = !document.dispatchEvent(new CustomEvent("storespos:cashout", { detail: result, cancelable: true }));
      if (!handled) {
        sessionStorage.setItem("storespos:flash", result.message);
        window.location.reload();
      }
    } catch {
      error.textContent = form.dataset.msgError;
    } finally {
      busy = false;
    }
  });

  // Flash message carried over the reload.
  try {
    const flash = sessionStorage.getItem("storespos:flash");
    if (flash) {
      sessionStorage.removeItem("storespos:flash");
      const box = document.createElement("div");
      box.className = "alert alert-success";
      box.setAttribute("role", "status");
      box.textContent = flash;
      const header = document.querySelector(".app-page-header");
      if (header) header.after(box);
    }
  } catch { /* storage unavailable */ }
})();
