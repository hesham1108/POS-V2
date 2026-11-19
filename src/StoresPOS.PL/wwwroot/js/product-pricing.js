(function () {
  function roundMoney(value) {
    return Math.round((value + Number.EPSILON) * 100) / 100;
  }

  function roundMarkup(value) {
    return Math.round((value + Number.EPSILON) * 10000) / 10000;
  }

  function num(el) {
    var v = parseFloat(el.value);
    return Number.isFinite(v) ? v : 0;
  }

  function setMoney(el, value) {
    el.value = roundMoney(value).toFixed(2);
  }

  function setMarkup(el, value) {
    el.value = roundMarkup(value).toFixed(4);
  }

  function sellFromMarkup(cost, markup) {
    return roundMoney(cost * (1 + markup / 100));
  }

  function markupFromSell(cost, sell) {
    if (cost === 0) return 0;
    return roundMarkup(((sell - cost) / cost) * 100);
  }

  var costEl = document.getElementById("PurchasePrice");
  var retailMarkupEl = document.getElementById("RetailMarkupPercent");
  var retailPriceEl = document.getElementById("RetailPrice");
  var wholesaleMarkupEl = document.getElementById("WholesaleMarkupPercent");
  var wholesalePriceEl = document.getElementById("WholesalePrice");

  if (!costEl || !retailMarkupEl || !retailPriceEl || !wholesaleMarkupEl || !wholesalePriceEl) {
    return;
  }

  var syncing = false;

  function fromRetailMarkup() {
    if (syncing) return;
    syncing = true;
    setMoney(retailPriceEl, sellFromMarkup(num(costEl), num(retailMarkupEl)));
    syncing = false;
  }

  function fromRetailPrice() {
    if (syncing) return;
    syncing = true;
    setMarkup(retailMarkupEl, markupFromSell(num(costEl), num(retailPriceEl)));
    syncing = false;
  }

  function fromWholesaleMarkup() {
    if (syncing) return;
    syncing = true;
    setMoney(wholesalePriceEl, sellFromMarkup(num(costEl), num(wholesaleMarkupEl)));
    syncing = false;
  }

  function fromWholesalePrice() {
    if (syncing) return;
    syncing = true;
    setMarkup(wholesaleMarkupEl, markupFromSell(num(costEl), num(wholesalePriceEl)));
    syncing = false;
  }

  function fromCost() {
    if (syncing) return;
    syncing = true;
    setMoney(retailPriceEl, sellFromMarkup(num(costEl), num(retailMarkupEl)));
    setMoney(wholesalePriceEl, sellFromMarkup(num(costEl), num(wholesaleMarkupEl)));
    syncing = false;
  }

  costEl.addEventListener("input", fromCost);
  retailMarkupEl.addEventListener("input", fromRetailMarkup);
  retailPriceEl.addEventListener("input", fromRetailPrice);
  wholesaleMarkupEl.addEventListener("input", fromWholesaleMarkup);
  wholesalePriceEl.addEventListener("input", fromWholesalePrice);
})();
