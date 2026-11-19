# STATUS.md — StoresPOS Implementation Status

## Project Status

- Project name: StoresPOS
- Master plan: `plan.md` (source of truth)
- Current phase: **Phase 6 complete** (2 items depend on Phase 7) — next is **Phase 7 (POS & Sales)**
- Overall progress: Phases 1–6 verified
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

## Pending Tasks

- Phase 6 carry-overs (need Phase 7): attribute sales to the shift; block close while another cashier has a Draft sale
- Phase 7 onward in `plan.md`
- Not yet checked on SQL Server / in a browser (dev Mac has no SQL Server): apply migration, eyeball gate/close screens in en + ar

## Tests & Build Results

| Command | Result |
| --- | --- |
| `dotnet build StoresPOS.sln` | Succeeded |
| `dotnet test tests/StoresPOS.BLL.Tests` | Passed 44/44 |
| `dotnet test tests/StoresPOS.PL.Tests` | Passed 3/3 |
| `dotnet ef database update` | `ShiftsAndPermissions` not yet applied to SQL Server |

## Next Recommended Task

Phase 7 — POS & Sales (start with the server-side `Draft` sales invoice)

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
| 2026-11-19 | Phase 6 shifts + permissions + ar/en localization groundwork |
| 2026-11-19 | `page-composition.md` rewritten as per-page build spec; `plan.md` gained rules 11–12, permission/localization/Draft-invoice tasks, new open decisions; sidebar reordered (+Shifts, +Cash-outs, Configuration→Settings) — build verified, BLL tests 29/29 |
| 2026-11-19 | UI parity pass: tokens, shell, shared components, Inventory→Products→rest; RTL logical props |

## Local Dev Login

- Username: `admin`
- Password: `Admin@12345`
- URL: http://127.0.0.1:5188
