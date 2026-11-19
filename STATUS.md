# STATUS.md — StoresPOS Implementation Status

## Project Status

- Project name: StoresPOS
- Master plan: `plan.md` (source of truth)
- Current phase: **Phase 8 complete** — next is **Phase 9 (cash-outs & returns)**
- Overall progress: Phases 1–8 verified (unit + HTTP tests; receipts checked visually; not yet on SQL Server)
- Latest verified build result: Succeeded
- Last update date: 2026-11-19

## Completed Tasks

### Phases 1–4

- Foundation, JWT auth, Products/Categories, Suppliers/Purchases

### Phase 5 — Inventory

- `StockMovement` ledger (Purchase / Sale / SaleReturn / Adjustment)
- Balances from `Product.StockQuantity` + low-stock filter/count
- Purchase confirm writes Purchase movements via `IInventoryService`
- Stock adjustment: delta (+/−) or set counted qty + required reason + user
- Blocks stock below zero
- PL: Inventory balances, movements, adjust screens
- Migration `InventoryStockMovements`
- Tests: 29/29

### Phase 6 — Shifts

- Permissions: `RolePermissions` table, `AppPermissions` keys, default matrix seeded (Cashier optional cells OFF; Admin always full set), `perm` claims in JWT, one policy per permission; existing controllers moved from roles to policies
- **Re-login required** after deploying: old tokens have no `perm` claims
- `Shift` + `ShiftParticipant`, filtered unique index `UX_Shifts_SingleOpen`, `RowVersion` for concurrent close
- `IShiftService`: gate state A/B/C (old = opened before today's local date), open (blank = 0), resume (idempotent), live summary, close (Matched / Surplus / Shortage / Not reconciled)
- PL: `Shifts/Gate`, `Shifts/Current`, `Shifts/Close`; cashiers routed to the gate after login; close → sign out → login (H) or back to gate (C)
- Localization: en/ar UI culture via cookie (formatting stays en-US), RTL + `bootstrap.rtl`, `Resources/SharedResource.ar.resx`, sidebar + shift screens translated, login toggle sets the cookie
- Migration `ShiftsAndPermissions`; local tool manifest `dotnet-tools.json` (`dotnet tool restore` → `dotnet ef`)
- Tests: BLL 44/44, new `StoresPOS.PL.Tests` HTTP journeys 3/3

### Phase 7 — POS & Sales

- `SalesInvoice` (Draft → Held → Completed / Cancelled) + `SalesInvoiceItem` snapshots (names, catalog price, charged price, override user, cost)
- One Draft per cashier (`UX_SalesInvoices_OneDraftPerCashier`); sequential numbers `S-000001` at payment (unique, retried on collision)
- `ISalesService`: search/scan, add, qty, remove, retail↔wholesale reprice (overrides kept), price override + discount checked against DB permissions, hold/resume, pay (cash + change, card, InstaPay, Vodafone Cash — one method per invoice), stock deduction via inventory ledger (`Sale` movements), invoice list/details
- Shift totals now real: cash vs non-cash, per cashier; close blocked while a Draft has items (closer can hold it); held + empty drafts cancelled at close
- PL: full-screen `Pos/Index` (`_PosLayout`, `pos.js`, `pos.css`) with F2/F4/F6/F7/F8/F9, +/−/Del/arrows; AJAX partials; `Sales/Index` + `Sales/Details`; gate now hands off to POS
- Arabic strings for all Phase 7 screens and messages
- Migration `SalesInvoices`
- Tests: BLL 61/61, PL HTTP 5/5

### Phase 8 — Receipts & printing

- `StoreSettings` (`appsettings` → `Store`): header, footer, receipt language (store default `ar`), Western/Arabic-Indic digits, VAT-included line (14%), paper width
- `Receipts/Sale/{id}` 80 mm receipt: store header, invoice meta, lines at charged prices, item count, subtotal/discount/total, VAT included, tender, change, footer, Code 128 barcode of the invoice number (offline SVG)
- `Receipts/Shift/{id}`: times, opened/closed by, participants, sales by method and by cashier, drawer, actual, difference, result
- Printing via hidden iframe + `window.print()`; Chrome `--kiosk-printing` setup in `docs/printing.md`
- POS: "Pay and print · F9" prints automatically; "Reprint last" in the header; Sales list/details reprint + preview
- Close → `Shifts/Closed/{id}` auto-prints the report → "Finish and sign out" (H) or "Open a new shift" (C); X report from Current shift
- Cashiers can only reprint invoices of the shift they are working in
- Store name from settings in sidebar and POS header
- Tests: BLL 61/61, PL 9/9 (receipt + shift report content, Code 128 table/checksum); receipt/report rendered and checked in a browser at 80 mm (Arabic)

## Pending Tasks

- Phase 9: cash-outs, returns (scan the receipt barcode to find the invoice)
- Silent printing on the real thermal printer not tested (no printer here) — follow `docs/printing.md` on the store PC
- Apply migrations `ShiftsAndPermissions` + `SalesInvoices` on SQL Server; click through POS in a browser (en + ar) — not possible on the dev Mac (no SQL Server)
- Client to confirm the POS open decisions applied as defaults (split tender, zero stock, held at close, tax)
- Error messages that name a product use the English name even in Arabic UI

## Tests & Build Results

| Command | Result |
| --- | --- |
| `dotnet build StoresPOS.sln` | Succeeded |
| `dotnet test tests/StoresPOS.BLL.Tests` | Passed 61/61 |
| `dotnet test tests/StoresPOS.PL.Tests` | Passed 9/9 |
| `dotnet ef database update` | `ShiftsAndPermissions`, `SalesInvoices` not yet applied to SQL Server |

## Next Recommended Task

Phase 9 — cash-outs (modal on POS + Current shift) and returns from the original invoice

## Design System Note (2026-11-19)

UI shell aligned to `docs/client-proposal-ar.html`: dark sidebar `#1A1D26`, primary `#0F6751`, offline fonts (Inter / Manrope / IBM Plex Sans Arabic / JetBrains Mono), soft panel shadows, 16px radius cards.

## Page Composition Spec (2026-11-19)

Canonical per-page layout/components for every sidebar tab: [`docs/page-composition.md`](docs/page-composition.md). Use it before UI rebuilds; no full mockup rebuild until explicitly requested.

## Change Log

| Date       | Change                                                                 |
| ---------- | ---------------------------------------------------------------------- |
| 2026-11-17 | Phase 1 foundation + Phase 2 auth                                      |
| 2026-11-17 | Login UI restyled from mockups; added `theme.css` design tokens        |
| 2026-11-18 | Expanded `docs/projectOverview.md` with project purpose and structure  |
| 2026-11-19 | `plan.md` master plan (P1–P11); Phase 3 products/categories            |
| 2026-11-19 | Phase 4 suppliers & purchases implemented and verified                 |
| 2026-11-19 | Phase 5 inventory (movements, adjust, low-stock) implemented           |
| 2026-11-19 | `docs/page-composition.md` from flows A–H                              |
| 2026-11-19 | Phase 8 receipts, shift report, printing workflow |
| 2026-11-19 | Phase 7 POS, sales invoices, shift totals + close guard |
| 2026-11-19 | Phase 6 shifts + permissions + ar/en localization groundwork |
| 2026-11-19 | `page-composition.md` rewritten as per-page build spec; `plan.md` gained rules 11–12, permission/localization/Draft-invoice tasks, new open decisions; sidebar reordered (+Shifts, +Cash-outs, Configuration→Settings) — build verified, BLL tests 29/29 |
| 2026-11-19 | UI parity pass: tokens, shell, shared components, Inventory→Products→rest; RTL logical props |

## Local Dev Login

- Username: `admin`
- Password: `Admin@12345`
- URL: http://127.0.0.1:5188
