// StoresPOS — cashier screen (journey D). Keyboard-first: the scanner/keyboard can do everything.
// Server renders the cart (_PosCart) and product grid (_PosProducts); this file only wires events.
(() => {
  const root = document.getElementById("pos-root");
  if (!root) return;

  const token = root.querySelector('input[name="__RequestVerificationToken"]').value;
  const cartEl = document.getElementById("pos-cart");
  const productsEl = document.getElementById("pos-products");
  const search = document.getElementById("pos-search");
  const msg = root.dataset;
  const money = new Intl.NumberFormat("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  const modals = {
    pay: bootstrap.Modal.getOrCreateInstance(document.getElementById("pay-modal")),
    discount: bootstrap.Modal.getOrCreateInstance(document.getElementById("discount-modal")),
    held: bootstrap.Modal.getOrCreateInstance(document.getElementById("held-modal")),
    customer: bootstrap.Modal.getOrCreateInstance(document.getElementById("customer-modal")),
    methods: bootstrap.Modal.getOrCreateInstance(document.getElementById("methods-modal"))
  };

  let categoryId = "";
  let selectedLineId = null;
  let busy = false;

  // ------------------------------------------------------------------ helpers

  // Shared top-centre toasts (wwwroot/js/toast.js). kind: "ok" | "error" | "warning" | "info".
  function toast(text, kind) {
    if (window.StoresToast) window.StoresToast(text, kind === "ok" ? "success" : kind);
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
  function cartSubtotal() { return parseFloat(cart().dataset.subtotal || "0"); }
  function cartEmpty() { return cart().dataset.empty === "true"; }

  function renderCart(html) {
    cartEl.innerHTML = html;
    const lines = cartEl.querySelectorAll(".pos-line");
    if (!cartEl.querySelector(`.pos-line[data-line-id="${selectedLineId}"]`)) {
      selectedLineId = lines.length ? lines[lines.length - 1].dataset.lineId : null;
    }
    markSelected();
    syncCheckout();
    syncInCart();
  }

  // "× N" badge on each product card that is already on the invoice.
  function syncInCart() {
    const qty = new Map();
    cartEl.querySelectorAll(".pos-line[data-product-id]").forEach((li) => {
      const id = li.dataset.productId;
      qty.set(id, (qty.get(id) || 0) + (parseFloat(li.dataset.qty) || 0));
    });
    productsEl.querySelectorAll(".pos-product[data-product-id]").forEach((card) => {
      const badge = card.querySelector("[data-incart]");
      const n = qty.get(card.dataset.productId) || 0;
      card.classList.toggle("is-incart", n > 0);
      if (!badge) return;
      badge.hidden = n <= 0;
      badge.textContent = n > 0 ? "× " + String(Math.round(n * 1000) / 1000) : "";
    });
  }

  // ------------------------------------------------------------------ receipt printing (one shared choice)

  // One shared receipt choice for every payment: 80 mm | A4 | none. Starts from the configured format.
  const PRINT_FORMATS = ["80mm", "A4", "none"];
  let printFormat = PRINT_FORMATS.includes(msg.printDefault) ? msg.printDefault : "80mm";

  function syncPrint() {
    document.querySelectorAll("[data-print-seg] [data-print]").forEach((b) => {
      const on = b.dataset.print === printFormat;
      b.classList.toggle("is-active", on);
      b.setAttribute("aria-checked", on ? "true" : "false");
    });
    const current = document.querySelector('.pos-print-menu [data-print="' + printFormat + '"]');
    document.querySelectorAll("[data-print-current]").forEach((el) => { el.textContent = current ? current.textContent : ""; });
    document.querySelectorAll(".pos-print-pick__btn").forEach((b) => b.classList.toggle("is-off", printFormat === "none"));
    const label = document.getElementById("pay-confirm-label");
    if (label) label.textContent = printFormat === "none" ? label.dataset.noprint : label.dataset.print;
  }

  document.addEventListener("click", (e) => {
    const b = e.target.closest("[data-print-seg] [data-print]");
    if (!b) return;
    e.preventDefault();
    e.stopPropagation(); // keep the dropdown open long enough to see the change
    printFormat = b.dataset.print;
    syncPrint();
  });

  function printReceipt(sale) {
    if (printFormat === "none" || !sale || !sale.invoiceId) return;
    const url = printFormat === "A4" ? (sale.receiptA4Url || sale.receiptUrl) : (sale.receipt80Url || sale.receiptUrl);
    if (url && window.StoresPrint) window.StoresPrint(url, { invoiceId: sale.invoiceId, format: printFormat });
  }

  // ------------------------------------------------------------------ checkout footer (static, outside the cart partial)

  const checkoutEl = document.getElementById("pos-checkout");
  const payAmountLabel = document.getElementById("pos-pay-amount");
  // The chosen payment method: { id, kind, name, icon } — the one and only place the cashier picks it.
  let payChoice = null;

  const KIND_CASH = "1";
  const KIND_CHEQUE = "5";

  function syncCheckout() {
    if (!checkoutEl) return;
    checkoutEl.querySelectorAll('[data-checkout="pay"]').forEach((b) => { b.disabled = cartEmpty(); });
    if (payAmountLabel) payAmountLabel.textContent = money.format(cartTotal());
  }

  function choiceFrom(button) {
    if (!button || !button.dataset.kind) return null;
    const icon = button.querySelector("svg");
    return {
      id: button.dataset.payMethod,
      kind: button.dataset.kind,
      name: button.dataset.name || button.textContent.trim(),
      icon: icon ? icon.outerHTML : ""
    };
  }

  // Highlights the bar button for the choice; a method that only lives in "Other" lights up the Other button with its name.
  function setPayChoice(choice) {
    if (!checkoutEl) return;
    payChoice = choice;
    const barButtons = checkoutEl.querySelectorAll(".pos-methods-bar [data-pay-method]");
    const inBar = choice && checkoutEl.querySelector(`.pos-methods-bar [data-pay-method="${CSS.escape(choice.id)}"]`);
    const otherBtn = checkoutEl.querySelector('.pos-methods-bar [data-pay-method="other"]');
    barButtons.forEach((b) => {
      const on = inBar ? b === inBar : b === otherBtn && !!choice;
      b.classList.toggle("is-active", on);
      b.setAttribute("aria-checked", on ? "true" : "false");
    });
    // The pay button names the selected method, so it is clear what "collect" will do.
    document.querySelectorAll("[data-pay-method-name]").forEach((el) => { el.innerHTML = choice ? choice.icon + "<span></span>" : ""; const s = el.querySelector("span"); if (s) s.textContent = choice.name; });
    const otherLabel = otherBtn && otherBtn.querySelector("[data-other-label]");
    if (otherLabel) otherLabel.textContent = choice && !inBar ? choice.name : otherLabel.dataset.default;
  }

  function resetPayMethod() {
    if (!checkoutEl) return;
    setPayChoice(choiceFrom(checkoutEl.querySelector(".pos-methods-bar [data-pay-method][data-kind]")));
  }

  if (checkoutEl) {
    resetPayMethod();
    checkoutEl.addEventListener("click", (e) => {
      const method = e.target.closest("[data-pay-method]");
      if (method) {
        if (method.dataset.payMethod === "other") modals.methods.show();
        else setPayChoice(choiceFrom(method));
        return;
      }

      const pay = e.target.closest('[data-checkout="pay"]');
      if (pay && !pay.disabled) openPay();
    });
  }

  // "Other": pick any active method. Picking only SELECTS it — the sale is collected only by the
  // pay button ("تحصيل · F9") or F9, for every method.
  document.getElementById("methods-list").addEventListener("click", (e) => {
    const b = e.target.closest("[data-pay-method]");
    if (!b) return;
    setPayChoice(choiceFrom(b));
    modals.methods.hide();
  });

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
      page = 1;
      paginate();
      syncInCart();
    } catch (error) {
      fail(error);
    }
  }

  // ------------------------------------------------------------------ pager (client side, over the loaded cards)

  const pagerEl = document.getElementById("pos-pager");
  const pageSize = pagerEl ? parseInt(pagerEl.dataset.pageSize || "16", 10) : 0;
  let page = 1;

  function paginate() {
    if (!pagerEl) return;
    const cards = Array.from(productsEl.querySelectorAll(".pos-product"));
    const pages = Math.max(1, Math.ceil(cards.length / pageSize));
    page = Math.min(Math.max(page, 1), pages);
    cards.forEach((card, i) => { card.hidden = Math.floor(i / pageSize) + 1 !== page; });

    pagerEl.hidden = cards.length === 0;
    const shown = cards.filter((c) => !c.hidden).length;
    document.getElementById("pos-pager-info").textContent = (pagerEl.dataset.msgShowing || "Showing {0} of {1} items")
      .replace("{0}", String(shown)).replace("{1}", String(cards.length));

    const numbers = document.getElementById("pos-pager-numbers");
    numbers.innerHTML = "";
    const from = Math.max(1, Math.min(page - 2, pages - 4));
    for (let n = from; n <= Math.min(pages, from + 4); n++) {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "num" + (n === page ? " is-active" : "");
      b.dataset.page = String(n);
      b.textContent = String(n);
      if (n === page) b.setAttribute("aria-current", "page");
      numbers.appendChild(b);
    }
    pagerEl.querySelector('[data-page="prev"]').disabled = page <= 1;
    pagerEl.querySelector('[data-page="next"]').disabled = page >= pages;
  }

  if (pagerEl) {
    pagerEl.addEventListener("click", (e) => {
      const b = e.target.closest("[data-page]");
      if (!b || b.disabled) return;
      const v = b.dataset.page;
      page = v === "prev" ? page - 1 : v === "next" ? page + 1 : parseInt(v, 10);
      paginate();
      productsEl.scrollTop = 0;
    });
  }

  // ------------------------------------------------------------------ header clock

  const clockEl = document.getElementById("pos-clock");
  function tickClock() {
    if (!clockEl) return;
    const now = new Date();
    const h = now.getHours();
    const h12 = h % 12 || 12;
    clockEl.textContent = `${h12}:${String(now.getMinutes()).padStart(2, "0")} ${h < 12 ? clockEl.dataset.am : clockEl.dataset.pm}`;
  }
  tickClock();
  setInterval(tickClock, 15000);

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
    productsEl.querySelectorAll(".pos-product.is-picked").forEach((c) => c.classList.remove("is-picked"));
    card.classList.add("is-picked");
    await cartAction("/Pos/Add", { productId: card.dataset.productId });
    focusSearch();
  });

  // ------------------------------------------------------------------ cart actions

  cartEl.addEventListener("click", async (e) => {
    if (e.target.closest(".pos-price-edit")) return; // clicks inside the inline price editor
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
        openInlinePrice(line, button);
        return;
      case "sale-type":
        if (await cartAction("/Pos/SaleType", { saleType: button.dataset.value })) loadProducts();
        break;
      case "cust-walkin":
        await setWalkIn("1");
        break;
      case "cust-wholesale":
        await setWalkIn("2");
        break;
      case "customer":
        openCustomer();
        return;
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
    }
    focusSearch();
  });

  // ------------------------------------------------------------------ inline price edit (click the unit price on a line)

  function openInlinePrice(line, button) {
    if (!line || cartEl.querySelector(".pos-price-edit")) return;
    const wrap = document.createElement("span");
    wrap.className = "pos-price-edit";
    const input = document.createElement("input");
    input.className = "num";
    input.inputMode = "decimal";
    input.autocomplete = "off";
    input.dir = "ltr";
    input.value = button.dataset.price;
    input.setAttribute("aria-label", button.title || "");
    const ok = document.createElement("button");
    ok.type = "button";
    ok.className = "pos-price-edit__ok";
    ok.textContent = "✓";
    ok.setAttribute("aria-label", "OK");
    const cancel = document.createElement("button");
    cancel.type = "button";
    cancel.className = "pos-price-edit__cancel";
    cancel.textContent = "✕";
    cancel.setAttribute("aria-label", "Cancel");
    wrap.append(input, ok, cancel);
    button.hidden = true;
    button.after(wrap);
    input.focus();
    input.select();

    let done = false;
    const close = () => {
      if (done) return;
      done = true;
      wrap.remove();
      button.hidden = false;
    };
    const save = async () => {
      const value = input.value.trim();
      if (!value || value === button.dataset.price) { close(); focusSearch(); return; }
      done = true;
      if (!(await cartAction("/Pos/Price", { lineId: line.dataset.lineId, price: value }))) {
        wrap.remove();
        button.hidden = false;
      }
      focusSearch();
    };
    input.addEventListener("keydown", (e) => {
      if (e.key === "Enter") { e.preventDefault(); save(); }
      else if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); close(); focusSearch(); }
    });
    ok.addEventListener("mousedown", (e) => e.preventDefault()); // keep focus so blur does not cancel first
    ok.addEventListener("click", save);
    cancel.addEventListener("mousedown", (e) => e.preventDefault());
    cancel.addEventListener("click", () => { close(); focusSearch(); });
    // Typing: digits and one decimal point only (same rule as the cash amount box).
    input.addEventListener("input", () => {
      const v = input.value
        .replace(/[\u0660-\u0669]/g, (d) => String(d.charCodeAt(0) - 0x0660))
        .replace(/[\u06F0-\u06F9]/g, (d) => String(d.charCodeAt(0) - 0x06F0))
        .replace(/[\u066B,\u060C]/g, ".")
        .replace(/[^0-9.]/g, "")
        .replace(/(\..*)\./g, "$1");
      if (v !== input.value) input.value = v;
    });
    input.addEventListener("blur", () => setTimeout(() => { if (!done) close(); }, 120));
  }

  // ------------------------------------------------------------------ discount (F6): quick chips + custom amount/percent

  const discountInput = document.getElementById("discount-value");
  const discountPreview = document.getElementById("discount-preview");
  const discountError = document.getElementById("discount-error");
  let discountMode = "amount";

  function setDiscountMode(mode) {
    discountMode = mode;
    document.querySelectorAll("[data-discount-mode]").forEach((x) => {
      const on = x.dataset.discountMode === mode;
      x.classList.toggle("is-active", on);
      x.setAttribute("aria-checked", on ? "true" : "false");
    });
    syncDiscountPreview();
  }

  function syncDiscountPreview() {
    const v = parseFloat(discountInput.value);
    if (isNaN(v) || v < 0) { discountPreview.textContent = ""; return; }
    const sub = cartSubtotal();
    const off = discountMode === "percent" ? sub * Math.min(v, 100) / 100 : Math.min(v, sub);
    discountPreview.textContent = (discountPreview.dataset.template || "{0}").replace("{0}", money.format(Math.max(sub - off, 0)));
  }

  function openDiscount() {
    const button = cartEl.querySelector('.pos-cart__actions [data-action="discount"]');
    if (!button || button.disabled) {
      if (button && button.title) toast(button.title, "error");
      return;
    }
    document.getElementById("discount-subtotal").textContent = money.format(cartSubtotal());
    discountInput.value = "";
    discountError.textContent = "";
    setDiscountMode("amount");
    document.getElementById("discount-remove").disabled = parseFloat(cart().dataset.discount || "0") <= 0;
    modals.discount.show();
    setTimeout(() => discountInput.focus(), 150);
  }

  async function applyDiscount(data) {
    if (busy) return;
    busy = true;
    discountError.textContent = "";
    try {
      renderCart(await post("/Pos/Discount", data));
      modals.discount.hide();
    } catch (error) {
      if (error && error.userFacing) discountError.textContent = error.message;
      else fail(error);
    } finally {
      busy = false;
    }
  }

  document.querySelectorAll("[data-discount-mode]").forEach((b) => {
    b.addEventListener("click", () => { setDiscountMode(b.dataset.discountMode); discountInput.focus(); });
  });
  discountInput.addEventListener("input", syncDiscountPreview);
  document.getElementById("discount-chips").addEventListener("click", (e) => {
    const b = e.target.closest("[data-discount-percent]");
    if (b) applyDiscount({ percent: b.dataset.discountPercent });
  });
  document.getElementById("discount-remove").addEventListener("click", () => applyDiscount({ amount: "0" }));
  document.getElementById("discount-form").addEventListener("submit", (e) => {
    e.preventDefault();
    const value = discountInput.value.trim() || "0";
    applyDiscount(discountMode === "percent" ? { percent: value } : { amount: value });
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
      loadProducts(); // a resumed invoice may use another sale type
      focusSearch();
    }
  });

  // ------------------------------------------------------------------ customer bar + picker (F4)

  // Walk-in (retail) or wholesale without a name: clear the customer, then set the sale type.
  async function setWalkIn(saleType) {
    if (busy) return;
    const c = cart();
    busy = true;
    try {
      if (c.dataset.customerId) renderCart(await post("/Pos/Customer", { customerId: "" }));
      if (cart().dataset.saleType !== saleType) {
        renderCart(await post("/Pos/SaleType", { saleType }));
        loadProducts(); // prices follow the sale type
      }
    } catch (error) {
      fail(error);
    } finally {
      busy = false;
    }
  }

  const customerSearch = document.getElementById("customer-search");
  const customerList = document.getElementById("customer-list");
  const customerError = document.getElementById("customer-error");
  const customerQuick = document.getElementById("customer-quick");
  const customerAddToggle = document.getElementById("customer-add-toggle");
  let customerTimer = null;
  let customerPage = 1;

  async function loadCustomers() {
    const params = new URLSearchParams({ term: customerSearch.value.trim(), page: String(customerPage) });
    try {
      customerList.innerHTML = await request("/Pos/Customers?" + params.toString());
    } catch (error) {
      fail(error);
    }
  }

  async function pickCustomer(customerId) {
    if (await cartAction("/Pos/Customer", { customerId: customerId || "" })) {
      modals.customer.hide();
      loadProducts(); // the customer's default sale type may have changed the prices
    }
  }

  function showQuickAdd(show) {
    customerQuick.hidden = !show;
    customerAddToggle.setAttribute("aria-expanded", show ? "true" : "false");
    customerAddToggle.classList.toggle("is-open", show);
    if (show) {
      const typed = customerSearch.value.trim();
      const nameInput = document.getElementById("quick-name");
      const phoneInput = document.getElementById("quick-phone");
      if (typed && !nameInput.value && !phoneInput.value) {
        if (/^[\d+\s-]+$/.test(typed)) phoneInput.value = typed; else nameInput.value = typed;
      }
      setTimeout(() => nameInput.focus(), 50);
    }
  }

  function openCustomer() {
    customerSearch.value = "";
    customerPage = 1;
    document.getElementById("quick-name").value = "";
    document.getElementById("quick-phone").value = "";
    customerError.textContent = "";
    setQuickType("1");
    showQuickAdd(false);
    loadCustomers();
    modals.customer.show();
    setTimeout(() => customerSearch.focus(), 150);
  }

  customerAddToggle.addEventListener("click", () => showQuickAdd(customerQuick.hidden));

  // Quick add: customer type (retail / wholesale / company).
  let quickType = "1";
  function setQuickType(value) {
    quickType = value;
    customerQuick.querySelectorAll("[data-quick-type]").forEach((b) => {
      const on = b.dataset.quickType === value;
      b.classList.toggle("is-active", on);
      b.setAttribute("aria-checked", on ? "true" : "false");
    });
  }
  customerQuick.addEventListener("click", (e) => {
    const b = e.target.closest("[data-quick-type]");
    if (b) setQuickType(b.dataset.quickType);
  });

  customerSearch.addEventListener("input", () => {
    clearTimeout(customerTimer);
    customerTimer = setTimeout(() => { customerPage = 1; loadCustomers(); }, 200);
  });

  // Enter picks the first match; arrows move through the list.
  customerSearch.addEventListener("keydown", (e) => {
    const first = customerList.querySelector("[data-customer-id]");
    if (e.key === "Enter") {
      e.preventDefault();
      if (first) pickCustomer(first.dataset.customerId);
    } else if (e.key === "ArrowDown" && first) {
      e.preventDefault();
      first.focus();
    }
  });

  customerList.addEventListener("keydown", (e) => {
    if (!["ArrowDown", "ArrowUp", "ArrowLeft", "ArrowRight"].includes(e.key)) return;
    const items = Array.from(customerList.querySelectorAll("[data-customer-id]"));
    const i = items.indexOf(document.activeElement);
    if (i < 0) return;
    e.preventDefault();
    const back = e.key === "ArrowUp" || e.key === "ArrowLeft";
    if (back && i === 0) customerSearch.focus();
    else items[Math.min(Math.max(i + (back ? -1 : 1), 0), items.length - 1)].focus();
  });

  customerList.addEventListener("click", (e) => {
    const pageBtn = e.target.closest("[data-customer-page]");
    if (pageBtn) {
      if (!pageBtn.disabled) {
        customerPage = parseInt(pageBtn.dataset.customerPage, 10) || 1;
        loadCustomers();
      }
      return;
    }
    const b = e.target.closest("[data-customer-id]");
    if (b) pickCustomer(b.dataset.customerId);
  });

  customerQuick.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (busy) return;
    const name = document.getElementById("quick-name").value.trim();
    const phone = document.getElementById("quick-phone").value.trim();
    customerError.textContent = "";
    busy = true;
    try {
      renderCart(await post("/Pos/QuickCustomer", { name, phone, customerType: quickType }));
      modals.customer.hide();
      loadProducts();
    } catch (error) {
      if (error && error.userFacing) customerError.textContent = error.message;
      else fail(error);
    } finally {
      busy = false;
    }
  });

  // ------------------------------------------------------------------ pay (F9)
  // Cash -> amount/change modal. Cheque -> cheque-details modal. Card / wallet / transfer -> paid directly, no modal.
  // Every path prints with the shared print choice (80 mm | A4 | none); there is no print dialog after paying.

  const payForm = document.getElementById("pay-form");
  const payAmount = document.getElementById("pay-amount");
  const payChange = document.getElementById("pay-change");
  const payChangeBox = document.getElementById("pay-change-box");
  const payChangeLabel = document.getElementById("pay-change-label");
  const payError = document.getElementById("pay-error");
  const payQuick = document.getElementById("pay-quick");
  let payFlow = null; // the choice the open modal is collecting for

  function chipHtml(choice) {
    if (!choice) return "";
    const span = document.createElement("span");
    span.textContent = choice.name;
    return choice.icon + span.outerHTML;
  }

  // Exact amount, then the next sensible notes above the total (e.g. 417.50 -> 420, 450, 500, 1000).
  function quickAmounts(total) {
    const steps = [10, 50, 100, 200, 500, 1000];
    const out = [];
    steps.forEach((s) => {
      const v = Math.ceil(total / s) * s;
      if (v > total + 0.001 && !out.includes(v)) out.push(v);
    });
    // Always offer four notes (one row), adding bigger round amounts when the total is high.
    for (let i = 0; out.length < 4 && i < 30; i++) {
      const v = [2, 5, 10][i % 3] * 1000 * Math.pow(10, Math.floor(i / 3));
      if (v > total && !out.includes(v)) out.push(v);
    }
    return out.sort((a, b) => a - b).slice(0, 4);
  }

  function renderQuick(total) {
    const exact = document.createElement("button");
    exact.type = "button";
    exact.className = "is-exact";
    exact.dataset.quick = "exact";
    exact.textContent = payQuick.dataset.exact || "Exact";
    payQuick.replaceChildren(exact);
    quickAmounts(total).forEach((v) => {
      const b = document.createElement("button");
      b.type = "button";
      b.className = "num";
      b.dataset.quick = String(v);
      b.textContent = String(v);
      payQuick.appendChild(b);
    });
  }

  function syncPay() {
    const paid = parseFloat(payAmount.value);
    const diff = isNaN(paid) ? -cartTotal() : Math.round((paid - cartTotal()) * 100) / 100;
    const short = diff < 0;
    payChange.textContent = money.format(Math.abs(diff));
    // Highlight the quick-amount button that matches what is typed.
    payQuick.querySelectorAll("[data-quick]").forEach((b) => {
      const v = b.dataset.quick === "exact" ? cartTotal() : parseFloat(b.dataset.quick);
      b.classList.toggle("is-active", !isNaN(paid) && Math.abs(v - paid) < 0.005);
    });
    payChangeBox.classList.toggle("is-short", short);
    payChangeLabel.textContent = short ? payChangeLabel.dataset.short : payChangeLabel.dataset.change;
  }

  function openPay() {
    if (cartEmpty() || busy) return;
    const choice = payChoice || choiceFrom(checkoutEl && checkoutEl.querySelector(".pos-methods-bar [data-kind]"));
    if (!choice) return;
    if (choice.kind !== KIND_CASH && choice.kind !== KIND_CHEQUE) {
      submitPay(choice, { method: choice.id, reference: "" });
      return;
    }

    payFlow = choice;
    const isCash = choice.kind === KIND_CASH;
    document.getElementById("pay-method").value = choice.id;
    document.getElementById("pay-method-chip").innerHTML = chipHtml(choice);
    document.getElementById("pay-confirm-chip").innerHTML = chipHtml(choice);
    document.getElementById("pay-cash").hidden = !isCash;
    document.getElementById("pay-cheque").hidden = isCash;
    document.getElementById("pay-due").textContent = money.format(cartTotal());
    document.getElementById("pay-due-cheque").textContent = money.format(cartTotal());
    payAmount.value = cartTotal().toFixed(2);
    document.getElementById("pay-cheque-number").value = "";
    document.getElementById("pay-cheque-bank").value = "";
    document.getElementById("pay-cheque-due").value = "";
    payError.textContent = "";
    renderQuick(cartTotal());
    syncPay();
    syncPrint();
    modals.pay.show(); // focus happens on "shown.bs.modal"
  }

  // ---- amount received: digits and one decimal point only (Arabic-Indic digits become 0-9)
  function cleanAmount(raw) {
    let v = String(raw || "")
      .replace(/[\u0660-\u0669]/g, (d) => String(d.charCodeAt(0) - 0x0660))
      .replace(/[\u06F0-\u06F9]/g, (d) => String(d.charCodeAt(0) - 0x06F0))
      .replace(/[\u066B,\u060C]/g, ".")
      .replace(/[^0-9.]/g, "");
    const dot = v.indexOf(".");
    if (dot >= 0) v = v.slice(0, dot + 1) + v.slice(dot + 1).replace(/\./g, "").slice(0, 2);
    return v;
  }

  payAmount.addEventListener("keydown", (e) => {
    if (e.ctrlKey || e.metaKey || e.altKey || e.key.length !== 1) return; // Enter, arrows, Backspace, shortcuts
    const ch = cleanAmount(e.key);
    if (!ch || (ch === "." && payAmount.value.includes(".") && !payAmount.value.slice(payAmount.selectionStart, payAmount.selectionEnd).includes("."))) {
      e.preventDefault();
    }
  });

  payAmount.addEventListener("input", () => {
    const cleaned = cleanAmount(payAmount.value);
    if (cleaned !== payAmount.value) {
      const pos = Math.max(0, (payAmount.selectionStart || cleaned.length) - (payAmount.value.length - cleaned.length));
      payAmount.value = cleaned;
      try { payAmount.setSelectionRange(pos, pos); } catch { /* not focused */ }
    }
    syncPay();
  });

  // Focus the first field once the modal is really shown, so the cashier can type straight away.
  document.getElementById("pay-modal").addEventListener("shown.bs.modal", () => {
    if (!payFlow) return;
    if (payFlow.kind === KIND_CASH) {
      payAmount.focus();
      payAmount.select();
    } else {
      document.getElementById("pay-cheque-number").focus();
    }
  });

  payQuick.addEventListener("click", (e) => {
    const b = e.target.closest("[data-quick]");
    if (!b) return;
    payAmount.value = b.dataset.quick === "exact" ? cartTotal().toFixed(2) : b.dataset.quick;
    syncPay();
    payAmount.focus();
  });

  // On-screen numpad for touch tills. The first key replaces the pre-filled amount.
  let numpadFresh = true;
  payAmount.addEventListener("focus", () => { numpadFresh = true; });
  document.getElementById("pay-numpad").addEventListener("click", (e) => {
    const b = e.target.closest("[data-key]");
    if (!b) return;
    const key = b.dataset.key;
    let v = payAmount.value;
    if (key === "back") {
      v = v.slice(0, -1);
    } else {
      if (numpadFresh) v = "";
      if (key === ".") { if (!v.includes(".")) v = (v || "0") + "."; }
      else if (!/\.\d{2}$/.test(v)) v += key;
    }
    numpadFresh = false;
    payAmount.value = v;
    syncPay();
  });

  async function submitPay(choice, data) {
    if (busy) return;
    busy = true;
    try {
      const result = JSON.parse(await post("/Pos/Pay", data));
      modals.pay.hide();
      toast((msg.msgPaid || "Paid · Invoice {0}").replace("{0}", result.invoiceNumber || ("#" + result.invoiceId)), "ok");
      const reprint = document.getElementById("pos-reprint");
      if (reprint) {
        const preferA4 = printFormat === "A4" || (printFormat === "none" && !!result.preferA4);
        reprint.dataset.printUrl = preferA4 ? (result.receiptA4Url || result.receiptUrl) : (result.receipt80Url || result.receiptUrl);
        reprint.dataset.invoiceId = result.invoiceId;
        reprint.dataset.printFormat = preferA4 ? "A4" : "80mm";
        reprint.disabled = false;
      }
      printReceipt(result);
      renderCart(await request("/Pos/Cart"));
      resetPayMethod();
      loadProducts();
      focusSearch();
    } catch (error) {
      const inModal = payFlow === choice && document.getElementById("pay-modal").classList.contains("show");
      if (error && error.userFacing && inModal) payError.textContent = error.message;
      else fail(error);
    } finally {
      busy = false;
    }
  }

  payForm.addEventListener("submit", (e) => {
    e.preventDefault();
    if (!payFlow) return;
    const data = { method: payFlow.id };
    if (payFlow.kind === KIND_CASH) {
      const raw = payAmount.value.trim();
      if (raw && isNaN(parseFloat(raw))) { payError.textContent = msg.msgNeedAmount; return; }
      data.amountPaid = raw;
    } else {
      data.chequeNumber = document.getElementById("pay-cheque-number").value.trim();
      data.chequeBank = document.getElementById("pay-cheque-bank").value.trim();
      data.chequeDueDate = document.getElementById("pay-cheque-due").value;
      if (!data.chequeNumber) { payError.textContent = msg.msgChequeNumber || "Cheque number is required."; return; }
      if (!data.chequeDueDate) { payError.textContent = msg.msgChequeDue || "Cheque due date is required."; return; }
    }
    submitPay(payFlow, data);
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
        case "F4": openCustomer(); break;
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
  paginate();
  syncPrint();
  focusSearch();

  // Messages from the previous page (e.g. "Shift #3 opened.") — shown here so they don't leak to the next page.
  if (msg.flashError) toast(msg.flashError, "error");
  else if (msg.flash) toast(msg.flash, "ok");
})();
