(function () {
  var tbody = document.querySelector("#purchase-lines tbody");
  var template = document.getElementById("purchase-line-template");
  var addBtn = document.getElementById("add-line");
  var subtotalEl = document.getElementById("subtotal");

  if (!tbody || !template || !addBtn) {
    return;
  }

  function money(n) {
    return (Math.round((n + Number.EPSILON) * 100) / 100).toFixed(2);
  }

  function reindex() {
    Array.prototype.forEach.call(tbody.querySelectorAll(".purchase-line"), function (row, index) {
      row.querySelectorAll("select, input").forEach(function (el) {
        if (!el.name) return;
        el.name = el.name.replace(/Items\[\d+\]/, "Items[" + index + "]");
      });
    });
  }

  function recalc() {
    var total = 0;
    Array.prototype.forEach.call(tbody.querySelectorAll(".purchase-line"), function (row) {
      var qty = parseFloat(row.querySelector(".qty-input").value) || 0;
      var cost = parseFloat(row.querySelector(".cost-input").value) || 0;
      var line = qty * cost;
      row.querySelector(".line-total").textContent = money(line);
      total += line;
    });
    if (subtotalEl) {
      subtotalEl.textContent = money(total);
    }
  }

  function bindRow(row) {
    var select = row.querySelector(".product-select");
    var costInput = row.querySelector(".cost-input");

    select.addEventListener("change", function () {
      var opt = select.options[select.selectedIndex];
      var cost = opt && opt.getAttribute("data-cost");
      if (cost != null && cost !== "") {
        costInput.value = parseFloat(cost).toFixed(2);
      }
      recalc();
    });

    row.querySelector(".qty-input").addEventListener("input", recalc);
    costInput.addEventListener("input", recalc);

    row.querySelector(".remove-line").addEventListener("click", function () {
      if (tbody.querySelectorAll(".purchase-line").length <= 1) {
        return;
      }
      row.remove();
      reindex();
      recalc();
    });
  }

  Array.prototype.forEach.call(tbody.querySelectorAll(".purchase-line"), bindRow);

  addBtn.addEventListener("click", function () {
    var html = template.innerHTML.replace(/__IX__/g, String(tbody.querySelectorAll(".purchase-line").length));
    var wrapper = document.createElement("tbody");
    wrapper.innerHTML = html.trim();
    var row = wrapper.firstElementChild;
    tbody.appendChild(row);
    bindRow(row);
    reindex();
    recalc();
  });

  recalc();
})();
