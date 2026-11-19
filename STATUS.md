# STATUS.md — StoresPOS Implementation Status

## Project Status

- Project name: StoresPOS
- Master plan: `plan.md` (source of truth)
- Current phase: **Phase 5 complete** — next is **Phase 6 (Shifts)**
- Overall progress: Phases 1–5 verified
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

## Pending Tasks

- Phase 6 onward in `plan.md`

## Tests & Build Results

| Command | Result |
| --- | --- |
| `dotnet build StoresPOS.sln` | Succeeded |
| `dotnet test tests/StoresPOS.BLL.Tests` | Passed 29/29 |
| `dotnet ef database update` | Applied `InventoryStockMovements` |

## Next Recommended Task

Phase 6 — Shifts

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
| 2026-11-19 | UI parity pass: tokens, shell, shared components, Inventory→Products→rest; RTL logical props |

## Local Dev Login

- Username: `admin`
- Password: `Admin@12345`
- URL: http://127.0.0.1:5188
