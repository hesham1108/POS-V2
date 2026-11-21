// Dashboard (Phase 22): draws the charts (Chart.js from wwwroot/lib, offline) and loads the
// analysis / money / activity rows after the page, so the key figures show first.
(function () {
  "use strict";
  var root = document.querySelector(".dash");
  if (!root) return;

  var i18n = {};
  try { i18n = JSON.parse(document.getElementById("dash-i18n").textContent || "{}"); } catch (e) { i18n = {}; }
  var isAr = i18n.lang === "ar";
  var rtl = document.documentElement.dir === "rtl";
  var css = getComputedStyle(document.documentElement);
  function token(name, fallback) { return (css.getPropertyValue(name) || "").trim() || fallback; }

  var colors = {
    primary: token("--sp-primary", "#1559d6"),
    previous: token("--sp-primary-border", "#b9cff8"),
    grid: token("--sp-border", "#e3e7ee"),
    text: token("--sp-text-muted", "#6b7280"),
    slices: [token("--sp-primary", "#1559d6"), token("--sp-success", "#16a34a"), token("--sp-warning", "#d97706"), "#7c3aed", "#0891b2", "#94a3b8"]
  };
  var money = new Intl.NumberFormat("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  var whole = new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 });

  function failed(el) {
    el.innerHTML = "";
    var p = document.createElement("p");
    p.className = "text-muted mb-0";
    p.textContent = i18n.failed || "";
    el.appendChild(p);
  }

  // ---------- Charts ----------
  function drawCharts(data) {
    if (!window.Chart) return;
    Chart.defaults.font.family = token("--sp-font", "sans-serif");
    Chart.defaults.color = colors.text;

    var bars = document.getElementById("dash-sales");
    if (bars) {
      new Chart(bars, {
        type: "bar",
        data: {
          labels: data.labels,
          datasets: [
            { label: i18n.previous, data: data.previous, backgroundColor: colors.previous, borderRadius: 4, maxBarThickness: 18 },
            { label: i18n.current, data: data.current, backgroundColor: colors.primary, borderRadius: 4, maxBarThickness: 18 }
          ]
        },
        options: {
          maintainAspectRatio: false,
          animation: false,
          interaction: { mode: "index", intersect: false },
          plugins: {
            legend: { display: false },
            tooltip: {
              rtl: rtl,
              callbacks: { label: function (c) { return " " + c.dataset.label + ": " + money.format(c.parsed.y); } }
            }
          },
          scales: {
            // Time runs right-to-left in Arabic.
            x: { reverse: rtl, grid: { display: false } },
            y: {
              position: rtl ? "right" : "left",
              beginAtZero: true,
              grid: { color: colors.grid },
              ticks: { callback: function (v) { return whole.format(v); } }
            }
          }
        }
      });
    }

    var donut = document.getElementById("dash-methods");
    var list = document.getElementById("dash-methods-list");
    var slices = data.paymentMethods || [];
    var total = slices.reduce(function (a, s) { return a + s.amount; }, 0);
    if (list) {
      list.innerHTML = "";
      if (slices.length === 0) {
        var empty = document.createElement("li");
        empty.className = "text-muted";
        empty.textContent = list.dataset.empty || "";
        list.appendChild(empty);
      }
      slices.forEach(function (s, i) {
        var li = document.createElement("li");
        var dot = document.createElement("i");
        dot.style.background = colors.slices[i % colors.slices.length];
        var name = document.createElement("span");
        name.textContent = isAr ? s.nameAr : s.nameEn;
        var value = document.createElement("span");
        value.className = "num";
        value.textContent = money.format(s.amount) + " · " + (total ? Math.round(s.amount / total * 100) : 0) + "%";
        li.appendChild(dot); li.appendChild(name); li.appendChild(value);
        list.appendChild(li);
      });
    }
    if (donut && slices.length > 0) {
      new Chart(donut, {
        type: "doughnut",
        data: {
          labels: slices.map(function (s) { return isAr ? s.nameAr : s.nameEn; }),
          datasets: [{ data: slices.map(function (s) { return s.amount; }), backgroundColor: slices.map(function (_, i) { return colors.slices[i % colors.slices.length]; }), borderWidth: 0 }]
        },
        options: {
          maintainAspectRatio: false,
          animation: false,
          cutout: "68%",
          plugins: {
            legend: { display: false },
            tooltip: { rtl: rtl, callbacks: { label: function (c) { return " " + c.label + ": " + money.format(c.parsed); } } }
          }
        }
      });
    } else if (donut) {
      donut.closest(".dash-chart").hidden = true;
    }
  }

  fetch(root.dataset.chartsUrl, { headers: { Accept: "application/json" } })
    .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
    .then(drawCharts)
    .catch(function () {
      var box = document.querySelector(".dash-row--charts");
      if (box) failed(box);
    });

  // ---------- Details (HTML partial) ----------
  var details = document.getElementById("dash-details");
  fetch(root.dataset.detailsUrl, { headers: { Accept: "text/html" } })
    .then(function (r) { if (!r.ok) throw new Error(r.status); return r.text(); })
    .then(function (html) { details.innerHTML = html; details.removeAttribute("aria-busy"); })
    .catch(function () { failed(details); details.removeAttribute("aria-busy"); });
})();
