// Settings page (Phase 18): printers loaded after the page, one lazy receipt preview,
// unsaved-changes tracking, double-submit guard and settings search.
(function () {
    'use strict';

    var i18n = {};
    try { i18n = JSON.parse(document.getElementById('settings-i18n').textContent || '{}'); } catch (e) { i18n = {}; }

    var form = document.getElementById('settings-form');
    var token = form ? (form.querySelector('input[name="__RequestVerificationToken"]') || {}).value : '';

    // ---------- Unsaved changes ----------
    var dirty = false;
    var submitting = false;
    var stateEl = document.getElementById('settings-dirty');
    var discardBtn = document.getElementById('settings-discard');
    var saveBar = document.getElementById('settings-savebar');

    function setDirty(value) {
        dirty = value;
        if (stateEl) stateEl.textContent = value ? stateEl.dataset.dirty : stateEl.dataset.clean;
        if (saveBar) saveBar.classList.toggle('is-dirty', value);
        if (discardBtn) discardBtn.disabled = !value;
    }

    if (form) {
        form.addEventListener('input', function (e) { if (!e.target.closest('[data-no-dirty]')) setDirty(true); });
        form.addEventListener('change', function (e) { if (!e.target.closest('[data-no-dirty]')) setDirty(true); });
        form.addEventListener('reset', function () {
            setTimeout(function () { setDirty(false); schedulePreview(0); }, 0);
        });
        form.addEventListener('submit', function (e) {
            if (submitting) { e.preventDefault(); return; }
            submitting = true;
            var btn = document.getElementById('settings-save');
            if (btn) {
                btn.disabled = true;
                btn.textContent = btn.dataset.saving || btn.textContent;
            }
        });
        window.addEventListener('beforeunload', function (e) {
            if (dirty && !submitting) {
                e.preventDefault();
                e.returnValue = i18n.leave || '';
                return i18n.leave || '';
            }
        });
        // Leaving through the settings navigation asks first (browsers ignore custom beforeunload text).
        document.querySelectorAll('.sp-settings__link').forEach(function (a) {
            a.addEventListener('click', function (e) {
                if (dirty && !window.confirm(i18n.leave || 'Leave?')) {
                    e.preventDefault();
                } else {
                    dirty = false;
                }
            });
        });
    }

    // ---------- Printers (AJAX, cached on the server) ----------
    var printerSelects = Array.prototype.slice.call(document.querySelectorAll('[data-printer-select]'));
    var printerGrid = document.querySelector('[data-printers-url]');
    var statusEl = document.getElementById('printer-status');

    function fillPrinters(data) {
        printerSelects.forEach(function (select) {
            var saved = select.dataset.saved || select.value || '';
            var first = select.options[0];
            select.innerHTML = '';
            select.appendChild(first);
            var names = (data.printers || []).slice();
            if (saved && names.indexOf(saved) < 0) names.unshift(saved);
            names.forEach(function (name) {
                var o = document.createElement('option');
                o.value = name;
                o.textContent = name;
                select.appendChild(o);
            });
            select.value = saved;
        });
        if (statusEl) {
            if (!data.isSupported) {
                statusEl.textContent = data.message || '';
            } else if (!data.printers || data.printers.length === 0) {
                statusEl.textContent = i18n.noPrinters || '';
            } else {
                statusEl.textContent = (statusEl.dataset.windowsDefault || '') + ': ' + (data.windowsDefault || '—');
            }
        }
    }

    function loadPrinters(refresh) {
        if (!printerGrid) return;
        if (statusEl) statusEl.textContent = statusEl.dataset.loading || '';
        var url = printerGrid.dataset.printersUrl + (refresh ? '?refresh=true' : '');
        fetch(url, { headers: { 'Accept': 'application/json' } })
            .then(function (res) { return res.ok ? res.json() : null; })
            .then(function (data) { if (data) fillPrinters(data); })
            .catch(function () { if (statusEl) statusEl.textContent = ''; });
    }

    printerSelects.forEach(function (select) {
        select.addEventListener('change', function () { select.dataset.saved = select.value; });
    });

    var refreshBtn = document.getElementById('printer-refresh');
    if (refreshBtn) refreshBtn.addEventListener('click', function () { loadPrinters(true); });

    document.querySelectorAll('[data-printer-test]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var out = document.getElementById('printer-test-result');
            var target = document.getElementById(btn.dataset.target);
            var body = new URLSearchParams();
            body.set('format', btn.dataset.format || '80mm');
            body.set('printerName', target ? target.value : '');
            body.set('__RequestVerificationToken', token || '');
            btn.disabled = true;
            fetch(printerGrid.dataset.testUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8' },
                body: body
            })
                .then(function (res) { return res.json(); })
                .then(function (data) {
                    if (!out) return;
                    out.textContent = data.message || (data.ok ? i18n.printed : i18n.failed);
                    out.classList.toggle('text-danger', !data.ok);
                    out.classList.toggle('text-muted', !!data.ok);
                })
                .catch(function (err) {
                    if (out) { out.textContent = String(err); out.classList.add('text-danger'); }
                })
                .finally(function () { btn.disabled = false; });
        });
    });

    if (printerGrid) {
        // After first paint, so the tab itself never waits for Windows.
        window.setTimeout(function () { loadPrinters(false); }, 0);
    }

    // ---------- One receipt preview, loaded only when visible ----------
    var preview = document.getElementById('receipt-preview');
    var previewTimer = null;

    function previewFormat() {
        var checked = document.querySelector('input[name="preview-format"]:checked');
        return checked ? checked.value : '80mm';
    }

    function previewQuery() {
        var q = new URLSearchParams();
        document.querySelectorAll('.preview-field').forEach(function (el) {
            var name = (el.getAttribute('name') || '').replace('Form.', '');
            if (!name) return;
            if (el.type === 'checkbox') q.set(name, el.checked ? 'true' : 'false');
            else q.set(name, el.value || '');
        });
        return q.toString();
    }

    function loadPreview() {
        if (!preview) return;
        var base = previewFormat() === 'A4' ? preview.dataset.urlA4 : preview.dataset.url80;
        preview.classList.toggle('is-a4', previewFormat() === 'A4');
        // Setting a new src cancels the iframe's previous navigation, so only the latest preview loads.
        preview.src = base + '?' + previewQuery();
    }

    function schedulePreview(delay) {
        if (!preview) return;
        window.clearTimeout(previewTimer);
        previewTimer = window.setTimeout(loadPreview, typeof delay === 'number' ? delay : 600);
    }

    if (preview) {
        document.querySelectorAll('.preview-field').forEach(function (el) {
            el.addEventListener('change', function () { schedulePreview(); });
            el.addEventListener('input', function () { schedulePreview(); });
        });
        document.querySelectorAll('input[name="preview-format"]').forEach(function (el) {
            el.setAttribute('data-no-dirty', '');
            el.addEventListener('change', function () { schedulePreview(0); });
        });
        if ('IntersectionObserver' in window) {
            var seen = false;
            var io = new IntersectionObserver(function (entries) {
                if (!seen && entries.some(function (en) { return en.isIntersecting; })) {
                    seen = true;
                    io.disconnect();
                    loadPreview();
                }
            });
            io.observe(preview);
        } else {
            loadPreview();
        }
    }

    // ---------- Settings search ----------
    var search = document.getElementById('settings-search');
    var results = document.getElementById('settings-search-results');
    var entries = [];
    try { entries = JSON.parse(search ? search.dataset.index || '[]' : '[]'); } catch (e) { entries = []; }

    function hrefFor(entry) {
        return search.dataset.baseUrl + '?tab=' + encodeURIComponent(entry.tab) + '#' + encodeURIComponent(entry.id);
    }

    function renderResults() {
        var q = (search.value || '').trim().toLowerCase();
        results.innerHTML = '';
        if (!q) { results.hidden = true; return; }
        var seen = {};
        var hits = entries.filter(function (en) {
            var key = en.tab + '|' + en.id + '|' + en.label;
            if (seen[key]) return false;
            seen[key] = true;
            return en.label.toLowerCase().indexOf(q) >= 0;
        }).slice(0, 12);
        if (hits.length === 0) {
            var empty = document.createElement('div');
            empty.className = 'sp-settings__result is-empty';
            empty.textContent = search.dataset.empty || '';
            results.appendChild(empty);
        }
        hits.forEach(function (en) {
            var a = document.createElement('a');
            a.className = 'sp-settings__result';
            a.setAttribute('role', 'option');
            a.href = hrefFor(en);
            a.textContent = en.label;
            var tabLink = document.querySelector('.sp-settings__link[href*="tab=' + en.tab + '"] span');
            if (tabLink) {
                var small = document.createElement('small');
                small.textContent = tabLink.textContent;
                a.appendChild(small);
            }
            a.addEventListener('click', function (e) {
                if (document.querySelector('.sp-settings').dataset.tab === en.tab) {
                    e.preventDefault();
                    results.hidden = true;
                    highlight(en.id);
                }
            });
            results.appendChild(a);
        });
        results.hidden = false;
    }

    function highlight(id) {
        var el = document.getElementById(id);
        if (!el) return;
        var target = el.closest('.col-md-4, .col-md-6, .col-md-8, .col-12, .sp-printer-row, section, .sp-card') || el;
        target.scrollIntoView({ behavior: 'smooth', block: 'center' });
        target.classList.add('sp-highlight');
        window.setTimeout(function () { target.classList.remove('sp-highlight'); }, 2000);
        if (el.focus && /INPUT|SELECT|TEXTAREA/.test(el.tagName)) el.focus({ preventScroll: true });
    }

    if (search && results) {
        search.addEventListener('input', renderResults);
        search.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
                var first = results.querySelector('a.sp-settings__result');
                if (first) { e.preventDefault(); first.click(); if (!results.hidden) window.location.href = first.href; }
            } else if (e.key === 'Escape') {
                search.value = '';
                results.hidden = true;
            }
        });
        document.addEventListener('click', function (e) {
            if (!e.target.closest('.sp-settings__search')) results.hidden = true;
        });
    }

    if (window.location.hash.length > 1) {
        window.setTimeout(function () { highlight(decodeURIComponent(window.location.hash.slice(1))); }, 50);
    }
})();
