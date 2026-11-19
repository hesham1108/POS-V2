# STATUS.md — StoresPOS Implementation Status

## Project Status

- Project name: StoresPOS
- Master plan: `plan.md` (source of truth)
- Current phase: **Phase 4 complete** — next is **Phase 5 (Inventory)**
- Overall progress: Phases 1–4 verified
- Latest verified build result: Succeeded
- Last update date: 2026-11-19

## Completed Tasks

### Phase 1 — Foundation

- Solution + PL/BLL/DAL + references
- EF Core SQL Server connection
- Folder structure
- Offline Bootstrap assets
- Build verified

### Phase 2 — Login & Roles

- Custom JWT auth, roles, login UI, seed admin, tests

### Phase 3 — Products & Categories

- Hierarchical categories, product CRUD, markup pricing, low-stock threshold

### Phase 4 — Suppliers & Purchases

- `Supplier` CRUD with confirmed purchase count/total
- `PurchaseInvoice` + `PurchaseInvoiceItem` (Draft / Confirmed)
- Auto invoice number `P-yyyyMMdd-####` (or manual unique)
- Line validation: qty > 0, cost ≥ 0, no duplicate products
- Confirm (or create+confirm): increase `StockQuantity`, set `PurchasePrice`, recalc retail/wholesale from saved markups
- Line snapshots (name AR/EN, unit, unit cost) preserve history
- Confirmed invoices locked from edit
- PL: Suppliers + Purchases screens, dynamic line form JS
- Migration `SuppliersAndPurchases`
- Unit tests for suppliers & purchases

## In Progress

- None

## Pending Tasks

- Phase 5 onward in `plan.md`

## Blockers

- None

## Tests & Build Results

| Command                                 | Result                                                               |
| --------------------------------------- | -------------------------------------------------------------------- |
| `dotnet build StoresPOS.sln`            | Succeeded (0 warnings)                                               |
| `dotnet test tests/StoresPOS.BLL.Tests` | Passed 24/24                                                         |
| `dotnet ef database update`             | Applied `SuppliersAndPurchases`                                      |

## Next Recommended Task

Phase 5 — Inventory (stock movements, balances, low-stock list, authorized adjustments).

## Change Log

| Date       | Change                                                     |
| ---------- | ---------------------------------------------------------- |
| 2026-11-17 | Phase 1 foundation + Phase 2 auth                          |
| 2026-11-19 | `plan.md` master plan (P1–P11); Phase 3 products/categories |
| 2026-11-19 | Phase 4 suppliers & purchases implemented and verified     |

## Local Dev Login (Development only)

- Username: `admin`
- Password: `Admin@12345`
- Run: `dotnet run --project src/StoresPOS.PL` (http://127.0.0.1:5188)
