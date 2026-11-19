// StoresPOS — cashier screen (journey D). Keyboard-first: the scanner/keyboard can do everything.
// Server renders the cart (_PosCart) and product grid (_PosProducts); this file only wires events.
(() => {
  const root = document.getElementById("pos-root");
  if (!root) return;

  const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
  const cartEl = document.getElementById("pos-cart");
  const productsEl = document.getElementById("pos-products");
  const search = document.getElementById("pos-search");
  const toastEl = document.getElementById("pos-toast");
  const msg = root.dataset;
  const money = new Intl.NumberFormat("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  const modals = {
    pay: bootstrap.Modal.getOrCreateInstance(document.getElementById("pay-modal")),
    price: bootstrap.Modal.getOrCreateInstance(document.getElementById("price-modal")),
    discount: bootstrap.Modal.getOrCreateInstance(document.getElementById("discount-modal")),
    held: bootstrap.Modal.getOrCreateInstance(document.getElementById("held-modal"))
  };

  let categoryId = "";
  let selectedLineId = null;
  let priceLineId = null;
  let discountMode = "amount";
  let busy = false;

  // ------------------------------------------------------------------ helpers

  let toastTimer = null;
  function toast(text, kind) {
    toastEl.textContent = text;
    toastEl.className = "pos-toast" + (kind === "error" ? " is-error" : " is-ok");
    toastEl.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toastEl.hidden = true; }, kind === "error" ? 5000 : 3500);
  }

  async function request(url, options) {
    const response = await fetch(url, Object.assign({ credentials: "same-origin" }, options));
    const redirect = response.headers.get("X-Redirect");
    if (redirect) {
      window.location.href = redirect;
      throw new Error("redirect");
    }
    if (response.status === 401 || response.redirected && response.url.includes("/Account/Login")) {
      window.location.reload();
      throw new Error("auth");
    }
    const text = await response.text();
    if (!response.ok) {
      const error = new Error(text || msg.msgError);
      error.userFacing = true;
      throw error;
    }
    return text;
  }

  function post(url, data) {
    const body = new URLSearchParams(data || {});
    return request(url, {
      method: "POST",
      headers: { "RequestVerificationToken": token, "Content-Type": "application/x-www-form-urlencoded" },
      body
    });
  }

  function fail(error) {
    if (error && error.userFacing) toast(error.message, "error");
    else if (error && error.message !== "redirect" && error.message !== "auth") toast(msg.msgError, "error");
  }

  function cart() { return cartEl.querySelector(".pos-cart"); }
  function cartTotal() { return parseFloat(cart().dataset.total || "0"); }
  function cartEmpty() { return cart().dataset.empty === "true"; }

  function renderCart(html) {
    cartEl.innerHTML = html;
    const lines = cartEl.querySelectorAll(".pos-line");
    if (!cartEl.querySelector(`.pos-line[data-line-id="${selectedLineId}"]`)) {
      selectedLineId = lines.length ? lines[lines.length - 1].dataset.lineId : null;
    }
    markSelected();
  }

  function markSelected() {
    cartEl.querySelectorAll(".pos-line").forEach((li) => {
      li.classList.toggle("is-selected", li.dataset.lineId === selectedLineId);
    });
  }

  async function cartAction(url, data) {
    if (busy) return;
    busy = true;
    try {
      renderCart(await post(url, data));
      return true;
    } catch (error) {
      fail(error);
      return false;
    } finally {
      busy = false;
    }
  }

  async function loadProducts() {
    const params = new URLSearchParams({ term: search.value.trim(), categoryId });
    try {
      productsEl.innerHTML = await request("/Pos/Products?" + params.toString());
    } catch (error) {
      fail(error);
    }
  }

  function focusSearch() {
    search.focus();
    search.select();
  }

  // ------------------------------------------------------------------ search / scan

  let searchTimer = null;
  search.addEventListener("input", () => {
    clearTimeout(searchTimer);
    searchTimer = setTimeout(loadProducts, 200);
  });

  search.addEventListener("keydown", async (e) => {
    if (e.key !== "Enter") return;
    e.preventDefault();
    const code = search.value.trim();
    if (!code) return;

    // Scanner sends the barcode + Enter. Fall back to a single visible search result.
    try {
      renderCart(await post("/Pos/Scan", { code }));
      search.value = "";
      loadProducts();
    } catch (error) {
      const cards = productsEl.querySelectorAll(".pos-product:not([disabled])");
      if (cards.length === 1 && error.userFacing) {
        await cartAction("/Pos/Add", { productId: cards[0].dataset.productId });
        search.value = "";
        loadProducts();
      } else {
        fail(error);
      }
    }
    focusSearch();
  });

  root.querySelector(".pos-pills").addEventListener("click", (e) => {
    const pill = e.target.closest("[data-category]");
    if (!pill) return;
    categoryId = pill.dataset.category;
    root.querySelectorAll(".pos-pills [data-category]").forEach((p) => p.classList.toggle("is-active", p === pill));
    loadProducts();
  });

  productsEl.addEventListener("click", async (e) => {
    const card = e.target.closest("[data-product-id]");
    if (!card || card.disabled) return;
    await cartAction("/Pos/Add", { productId: card.dataset.productId });
    focusSearch();
  });

  // ------------------------------------------------------------------ cart actions

  cartEl.addEventListener("click", async (e) => {
    const line = e.target.closest(".pos-line");
    if (line) {
      selectedLineId = line.dataset.lineId;
      markSelected();
    }

    const button = e.target.closest("[data-action]");
    if (!button || button.disabled) return;

    switch (button.dataset.action) {
      case "qty":
        await cartAction("/Pos/Quantity", { lineId: line.dataset.lineId, delta: button.dataset.delta });
        break;
      case "remove":
        await cartAction("/Pos/Remove", { lineId: line.dataset.lineId });
        break;
      case "price":
        openPrice(line.dataset.lineId, button.dataset.price);
        return;
      case "sale-type":
        if (await cartAction("/Pos/SaleType", { saleType: button.dataset.value })) loadProducts();
        break;
      case "customer":
        toast(msg.msgCustomer, "ok");
        break;
      case "discount":
        openDiscount();
        return;
      case "hold":
        await cartAction("/Pos/Hold");
        break;
      case "held":
        openHeld();
        return;
      case "clear":
        if (window.confirm(msg.msgConfirmClear)) await cartAction("/Pos/Clear");
        break;
      case "pay":
        openPay();
        return;
    }
    focusSearch();
  });

  // ------------------------------------------------------------------ price override

  function openPrice(lineId, price) {
    priceLineId = lineId;
    const input = document.getElementById("price-value");
    input.value = price;
    modals.price.show();
    setTimeout(() => { input.focus(); input.select(); }, 150);
  }

  document.getElementById("price-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    if (await cartAction("/Pos/Price", { lineId: priceLineId, price: document.getElementById("price-value").value.trim() })) {
      modals.price.hide();
    }
  });

  // ------------------------------------------------------------------ discount (F6)

  function openDiscount() {
    const button = cartEl.querySelector('[data-action="discount"]');
    if (!button || button.disabled) {
      if (button && button.title) toast(button.title, "error");
      return;
    }
    const input = document.getElementById("discount-value");
    input.value = "";
    modals.discount.show();
    setTimeout(() => input.focus(), 150);
  }

  document.querySelectorAll("[data-discount-mode]").forEach((b) => {
    b.addEventListener("click", () => {
      discountMode = b.dataset.discountMode;
      document.querySelectorAll("[data-discount-mode]").forEach((x) => x.classList.toggle("is-active", x === b));
      document.getElementById("discount-value").focus();
    });
  });

  document.getElementById("discount-form").addEventListener("submit", async (e) => {
    e.preventDefault();
    const value = document.getElementById("discount-value").value.trim() || "0";
    const data = discountMode === "percent" ? { percent: value } : { amount: value };
    if (await cartAction("/Pos/Discount", data)) modals.discount.hide();
  });

  // ------------------------------------------------------------------ held (F7)

  async function openHeld() {
    try {
      document.getElementById("held-list").innerHTML = await request("/Pos/Held");
      modals.held.show();
    } catch (error) {
      fail(error);
    }
  }

  document.getElementById("held-list").addEventListener("click", async (e) => {
    const button = e.target.closest("[data-resume-id]");
    if (!button) return;
    if (await cartAction("/Pos/Resume", { invoiceId: button.dataset.resumeId })) {
      modals.held.hide();
      focusSearch();
    }
  });

  // ------------------------------------------------------------------ pay (F9)

  const payForm = document.getElementById("pay-form");
  const payAmount = document.getElementById("pay-amount");
  const payChange = document.getElementById("pay-change");
  const payError = document.getElementById("pay-error");

  function payMethod() { return payForm.querySelector('input[name="method"]:checked').value; }

  function syncPay() {
    const isCash = payMethod() === "1";
    document.getElementById("pay-cash").hidden = !isCash;
    document.getElementById("pay-noncash").hidden = isCash;
    const paid = parseFloat(payAmount.value);
    const change = isNaN(paid) ? 0 : Math.round((paid - cartTotal()) * 100) / 100;
    payChange.textContent = money.format(Math.max(change, 0));
    payChange.classList.toggle("text-danger", change < 0);
  }

  function openPay() {
    if (cartEmpty()) return;
    document.getElementById("pay-due").textContent = money.format(cartTotal());
    payForm.querySelector('input[name="method"][value="1"]').checked = true;
    payAmount.value = cartTotal().toFixed(2);
    document.getElementById("pay-reference").value = "";
    payError.textContent = "";
    syncPay();
    modals.pay.show();
    setTimeout(() => { payAmount.focus(); payAmount.select(); }, 150);
  }

  payForm.addEventListener("change", syncPay);
  payAmount.addEventListener("input", syncPay);

  document.getElementById("pay-quick").addEventListener("click", (e) => {
    const b = e.target.closest("[data-quick]");
    if (!b) return;
    payAmount.value = b.dataset.quick === "exact" ? cartTotal().toFixed(2) : b.dataset.quick;
    syncPay();
    payAmount.focus();
  });

  payForm.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (busy) return;
    const method = payMethod();
    const data = { method };
    if (method === "1") {
      const raw = payAmount.value.trim();
      if (raw && isNaN(parseFloat(raw))) { payError.textContent = msg.msgNeedAmount; return; }
      data.amountPaid = raw;
    } else {
      data.reference = document.getElementById("pay-reference").value.trim();
    }

    busy = true;
    try {
      const result = JSON.parse(await post("/Pos/Pay", data));
      modals.pay.hide();
      toast(result.message, "ok");
      if (result.receiptUrl && window.StoresPrint) {
        window.StoresPrint(result.receiptUrl);
        const reprint = document.getElementById("pos-reprint");
        if (reprint) { reprint.dataset.printUrl = result.receiptUrl; reprint.disabled = false; }
      }
      renderCart(await request("/Pos/Cart"));
      loadProducts();
      focusSearch();
    } catch (error) {
      if (error && error.userFacing) payError.textContent = error.message;
      else fail(error);
    } finally {
      busy = false;
    }
  });

  // ------------------------------------------------------------------ keyboard

  document.querySelectorAll(".modal").forEach((m) => m.addEventListener("hidden.bs.modal", focusSearch));

  // Cash-out saved from the header modal (cashout.js): show the toast instead of reloading the till.
  document.addEventListener("storespos:cashout", (e) => {
    e.preventDefault();
    toast(e.detail.message, e.detail.warning ? "error" : "ok");
    focusSearch();
  });

  function anyModalOpen() { return document.querySelector(".modal.show") !== null; }

  document.addEventListener("keydown", (e) => {
    const inField = e.target instanceof Element && e.target.matches("input, textarea, select");
    const key = e.key;

    if (["F2", "F4", "F6", "F7", "F8", "F9"].includes(key)) {
      e.preventDefault();
      if (anyModalOpen()) return;
      switch (key) {
        case "F2": focusSearch(); break;
        case "F4": toast(msg.msgCustomer, "ok"); break;
        case "F6": openDiscount(); break;
        case "F7": if (!cartEl.querySelector('[data-action="held"]').disabled) openHeld(); break;
        case "F8": if (!cartEmpty()) cartAction("/Pos/Hold"); break;
        case "F9": openPay(); break;
      }
      return;
    }

    if (anyModalOpen() || !selectedLineId) return;

    // +/−/Del act on the selected line; inside the empty search box too, so the cashier never needs the mouse.
    const searchIsEmpty = e.target === search && search.value === "";
    if (inField && !searchIsEmpty) return;

    if (key === "+" || key === "=") {
      e.preventDefault();
      cartAction("/Pos/Quantity", { lineId: selectedLineId, delta: 1 });
    } else if (key === "-") {
      e.preventDefault();
      cartAction("/Pos/Quantity", { lineId: selectedLineId, delta: -1 });
    } else if (key === "Delete") {
      e.preventDefault();
      cartAction("/Pos/Remove", { lineId: selectedLineId });
    } else if (key === "ArrowUp" || key === "ArrowDown") {
      const lines = Array.from(cartEl.querySelectorAll(".pos-line"));
      const i = lines.findIndex((l) => l.dataset.lineId === selectedLineId);
      const next = lines[Math.min(Math.max(i + (key === "ArrowUp" ? -1 : 1), 0), lines.length - 1)];
      if (next) { e.preventDefault(); selectedLineId = next.dataset.lineId; markSelected(); }
    }
  });

  renderCart(cartEl.innerHTML);
  focusSearch();

  // Messages from the previous page (e.g. "Shift #3 opened.") — shown here so they don't leak to the next page.
  if (msg.flashError) toast(msg.flashError, "error");
  else if (msg.flash) toast(msg.flash, "ok");
})();
