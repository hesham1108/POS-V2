// Users → edit page (Phase 21): tabs, grouped permission matrix with "from role" vs "custom",
// reset to role, search, select all per group and a live preview of the sidebar the user will see.
(function () {
  "use strict";
  var form = document.getElementById("user-form");
  if (!form) return;

  // ---------- Tabs (details/permissions switch without reload so unsaved edits stay) ----------
  var tabField = document.getElementById("user-tab-field");
  var formTabs = ["details", "permissions"];
  function showTab(key) {
    document.querySelectorAll("[data-user-panel]").forEach(function (p) { p.hidden = p.dataset.userPanel !== key; });
    document.querySelectorAll("[data-user-tab]").forEach(function (a) {
      var on = a.dataset.userTab === key;
      a.classList.toggle("is-active", on);
      a.setAttribute("aria-selected", on ? "true" : "false");
    });
    var actions = document.querySelector("[data-user-panel-actions]");
    if (actions) actions.hidden = formTabs.indexOf(key) < 0;
    if (tabField) tabField.value = key;
  }
  document.querySelectorAll("[data-user-tab]").forEach(function (a) {
    a.addEventListener("click", function (e) {
      var key = a.dataset.userTab;
      var current = tabField ? tabField.value : "details";
      // Security/activity are separate pages for saved users; details ↔ permissions switch in place.
      if (formTabs.indexOf(key) >= 0 && formTabs.indexOf(current) >= 0) {
        e.preventDefault();
        showTab(key);
        history.replaceState(null, "", a.getAttribute("href"));
      } else if (a.getAttribute("href").charAt(0) === "#") {
        e.preventDefault();
        showTab(key);
      }
    });
  });

  // ---------- Permission matrix ----------
  var rolePerms = {};
  try { rolePerms = JSON.parse(form.dataset.rolePerms || "{}"); } catch (e) { rolePerms = {}; }
  var sidebar = [];
  try { sidebar = JSON.parse(form.dataset.sidebar || "[]"); } catch (e) { sidebar = []; }

  var roleSelect = document.getElementById("role-select");
  var custom = document.getElementById("use-custom");
  var hint = document.getElementById("perm-mode-hint");
  var boxes = Array.prototype.slice.call(form.querySelectorAll('input[name="SelectedPermissions"]'));

  function roleSet() {
    var list = rolePerms[roleSelect ? roleSelect.value : ""] || [];
    var set = {};
    list.forEach(function (p) { set[p] = true; });
    return set;
  }

  function refresh() {
    var fromRole = roleSet();
    var isCustom = custom && custom.checked;
    if (hint) hint.textContent = isCustom ? hint.dataset.custom : hint.dataset.role;
    boxes.forEach(function (b) {
      if (!isCustom) b.checked = !!fromRole[b.value];
      b.disabled = !isCustom;
      var item = b.closest("[data-perm-item]");
      var differs = isCustom && b.checked !== !!fromRole[b.value];
      item.classList.toggle("is-custom", differs);
      var badge = item.querySelector(".sp-perm-source");
      if (badge) badge.textContent = differs ? badge.dataset.sourceCustom : (b.checked ? badge.dataset.sourceRole : "");
      var reset = item.querySelector("[data-perm-reset]");
      if (reset) reset.hidden = !differs;
    });
    document.querySelectorAll("[data-group-all]").forEach(function (all) {
      var group = all.closest("[data-perm-group]");
      var inGroup = group.querySelectorAll('input[name="SelectedPermissions"]');
      var checked = group.querySelectorAll('input[name="SelectedPermissions"]:checked');
      all.checked = inGroup.length > 0 && checked.length === inGroup.length;
      all.indeterminate = checked.length > 0 && checked.length < inGroup.length;
      all.disabled = !isCustom;
    });
    renderPreview();
  }

  function renderPreview() {
    var list = document.getElementById("perm-preview");
    if (!list) return;
    var on = {};
    boxes.forEach(function (b) { if (b.checked) on[b.value] = true; });
    list.innerHTML = "";
    var shown = sidebar.filter(function (s) { return on[s.p]; });
    if (shown.length === 0) {
      var li = document.createElement("li");
      li.className = "text-muted";
      li.textContent = list.dataset.empty || "";
      list.appendChild(li);
    }
    shown.forEach(function (s) {
      var li = document.createElement("li");
      li.textContent = s.label;
      list.appendChild(li);
    });
  }

  if (roleSelect) roleSelect.addEventListener("change", refresh);
  if (custom) custom.addEventListener("change", function () {
    if (!custom.checked) {
      var fromRole = roleSet();
      boxes.forEach(function (b) { b.checked = !!fromRole[b.value]; });
    }
    refresh();
  });
  boxes.forEach(function (b) { b.addEventListener("change", refresh); });

  document.querySelectorAll("[data-perm-reset]").forEach(function (btn) {
    btn.addEventListener("click", function () {
      var fromRole = roleSet();
      var box = form.querySelector('input[name="SelectedPermissions"][value="' + btn.dataset.permReset + '"]');
      if (box) box.checked = !!fromRole[box.value];
      form.dispatchEvent(new Event("change"));
      refresh();
    });
  });

  var resetAll = document.getElementById("perm-reset-all");
  if (resetAll) resetAll.addEventListener("click", function () {
    if (custom) custom.checked = false;
    form.dispatchEvent(new Event("change"));
    refresh();
  });

  document.querySelectorAll("[data-group-all]").forEach(function (all) {
    all.addEventListener("change", function () {
      all.closest("[data-perm-group]").querySelectorAll('input[name="SelectedPermissions"]').forEach(function (b) {
        if (!b.disabled) b.checked = all.checked;
      });
      form.dispatchEvent(new Event("change"));
      refresh();
    });
  });

  var search = document.getElementById("perm-search");
  if (search) search.addEventListener("input", function () {
    var q = search.value.trim().toLowerCase();
    document.querySelectorAll("[data-perm-item]").forEach(function (item) {
      item.hidden = q !== "" && item.dataset.search.toLowerCase().indexOf(q) < 0;
    });
    document.querySelectorAll("[data-perm-group]").forEach(function (g) {
      g.hidden = g.querySelectorAll("[data-perm-item]:not([hidden])").length === 0;
    });
  });

  // Disabled boxes are not posted; when custom is off the role's set applies anyway.
  form.addEventListener("submit", function () {
    if (custom && !custom.checked) boxes.forEach(function (b) { b.disabled = true; });
  });

  refresh();
})();
