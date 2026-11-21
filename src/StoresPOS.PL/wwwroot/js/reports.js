// Report charts (Phase 23): every <canvas data-report-chart="{json}"> is drawn with the local Chart.js.
(function () {
  "use strict";
  if (!window.Chart) return;
  var css = getComputedStyle(document.documentElement);
  function token(name, fallback) { return (css.getPropertyValue(name) || "").trim() || fallback; }
  var rtl = document.documentElement.dir === "rtl";
  var palette = [token("--sp-primary", "#1559d6"), token("--sp-success", "#16a34a"), token("--sp-warning", "#d97706"), "#7c3aed", "#0891b2", token("--sp-danger", "#dc2626"), "#94a3b8"];
  var muted = token("--sp-primary-border", "#b9cff8");
  var grid = token("--sp-border", "#e3e7ee");
  var money = new Intl.NumberFormat("en-US", { maximumFractionDigits: 2 });
  Chart.defaults.font.family = token("--sp-font", "sans-serif");
  Chart.defaults.color = token("--sp-text-muted", "#6b7280");

  document.querySelectorAll("canvas[data-report-chart]").forEach(function (canvas) {
    var cfg;
    try { cfg = JSON.parse(canvas.dataset.reportChart); } catch (e) { return; }
    var donut = cfg.type === "doughnut";
    var colorIndex = 0;
    var datasets = cfg.series.map(function (s) {
      // Muted series (the previous period) are pale; the others take the palette in order, primary first.
      var color = s.muted ? muted : palette[(colorIndex++) % palette.length];
      return {
        label: s.label,
        data: s.values,
        backgroundColor: donut ? cfg.labels.map(function (_, j) { return palette[j % palette.length]; }) : color,
        borderColor: color,
        borderWidth: cfg.type === "line" ? 2 : 0,
        borderRadius: cfg.type === "bar" ? 4 : 0,
        maxBarThickness: 26,
        tension: 0.25,
        pointRadius: cfg.type === "line" ? 2 : 0
      };
    });
    var valueAxis = {
      beginAtZero: true,
      grid: { color: grid },
      position: cfg.horizontal ? undefined : (rtl ? "right" : "left"),
      ticks: { callback: function (v) { return money.format(v); } }
    };
    var labelAxis = { grid: { display: false }, reverse: rtl && cfg.timeAxis && !cfg.horizontal };
    new Chart(canvas, {
      type: cfg.type,
      data: { labels: cfg.labels, datasets: datasets },
      options: {
        indexAxis: cfg.horizontal ? "y" : "x",
        maintainAspectRatio: false,
        animation: false,
        plugins: {
          legend: { display: donut || datasets.length > 1, position: "bottom", rtl: rtl },
          tooltip: { rtl: rtl, callbacks: { label: function (c) { var v = cfg.horizontal ? c.parsed.x : (donut ? c.parsed : c.parsed.y); return " " + (donut ? c.label : c.dataset.label) + ": " + money.format(v); } } }
        },
        scales: donut ? {} : (cfg.horizontal ? { x: valueAxis, y: { grid: { display: false }, position: rtl ? "right" : "left" } } : { x: labelAxis, y: valueAxis })
      }
    });
  });
})();
