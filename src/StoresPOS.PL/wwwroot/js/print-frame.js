// Prints a receipt/report without leaving the page: loads it in a hidden iframe with ?autoprint=1.
// Buttons opt in with data-print-url="/Receipts/Sale/12". Also exposed as window.StoresPrint(url).
(() => {
  function print(url) {
    if (!url) return;
    const old = document.getElementById("print-frame");
    if (old) old.remove();
    const frame = document.createElement("iframe");
    frame.id = "print-frame";
    frame.title = "print";
    frame.setAttribute("aria-hidden", "true");
    frame.style.cssText = "position:fixed;inset-inline-end:0;bottom:0;width:0;height:0;border:0;visibility:hidden";
    frame.src = url + (url.includes("?") ? "&" : "?") + "autoprint=1";
    document.body.appendChild(frame);
  }

  window.StoresPrint = print;

  document.addEventListener("click", (e) => {
    const button = e.target.closest("[data-print-url]");
    if (!button || button.disabled) return;
    e.preventDefault();
    print(button.dataset.printUrl);
  });

  const auto = document.querySelector("[data-print-on-load]");
  if (auto) window.addEventListener("load", () => print(auto.dataset.printOnLoad));
})();
