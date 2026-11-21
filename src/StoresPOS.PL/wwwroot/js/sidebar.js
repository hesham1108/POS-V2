// Off-canvas sidebar on narrow screens.
(function () {
  document.addEventListener("click", function (e) {
    if (e.target.closest("[data-sidebar-toggle]")) {
      document.body.classList.toggle("sidebar-open");
    } else if (document.body.classList.contains("sidebar-open") && e.target.closest(".app-sidebar__link, .pos-rail__link")) {
      document.body.classList.remove("sidebar-open");
    }
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") document.body.classList.remove("sidebar-open");
  });
})();
