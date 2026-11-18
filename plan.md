# PLANB.md — StoresPOS Master Development Plan

## Project Decisions

- Framework: ASP.NET Core MVC
- Architecture: 3-Tier (PL, BLL, DAL)
- Database: SQL Server / SQL Server Express
- Runtime: Local and Offline
- Authentication: Custom JWT Authentication (JWT stored in HttpOnly cookie for MVC)
- ASP.NET Core Identity: Not Used
- Initial Roles: Admin, Manager, Cashier
- Shift Model: One Open Shift, Multiple Cashiers
- UI Design: Design-agnostic functional requirements
- Languages: Arabic and English

## Phase 0 — Foundation

- [x] Create StoresPOS solution.
- [x] Create StoresPOS.PL MVC project.
- [x] Create StoresPOS.BLL Class Library.
- [x] Create StoresPOS.DAL Class Library.
- [x] Configure project references.
- [x] Configure dependency injection.
- [x] Configure EF Core and SQL Server.
- [x] Create base layout and shared components.
- [x] Configure offline static assets.
- [x] Verify successful build.

## Phase 1 — Custom JWT Authentication

- [x] Create custom Users and Roles tables.
- [x] Create User and Role entities.
- [x] Implement secure password hashing using PBKDF2.
- [x] Create a simple Login page (Username + Password).
- [x] Implement Login and Logout.
- [x] Implement JWT generation and validation.
- [x] Configure JWT Bearer Authentication.
- [x] Implement role-based authorization.
- [x] Create three initial roles: Admin, Manager, Cashier.
- [x] Create secure initial Admin setup.
- [x] Handle token expiration and unauthorized access.
- [x] Implement secure token storage and handling for MVC.
- [x] Protect Controllers and sensitive endpoints.
- [x] Test authentication, authorization, expired tokens, and logout.

## Phase 2 — Products & Categories

- [ ] Implement hierarchical categories.
- [ ] Implement Product entity and CRUD.
- [ ] Add Arabic and English names.
- [ ] Support barcode and unit.
- [ ] Add purchase price.
- [ ] Add retail and wholesale selling prices.
- [ ] Implement markup calculations.
- [ ] Implement low-stock thresholds.

## Phase 3 — Suppliers & Purchases

- [ ] Create Supplier management.
- [ ] Create Purchase Invoice and items.
- [ ] Validate quantities and costs.
- [ ] Increase inventory on confirmed purchase.
- [ ] Recalculate product selling prices using markup.
- [ ] Preserve purchase history.

## Phase 4 — Inventory

- [ ] Create stock movement records.
- [ ] Implement stock balance calculations.
- [ ] Implement low-stock notifications.
- [ ] Implement stock adjustments.
- [ ] Track the user performing adjustments.

## Phase 5 — Simple Shift Management

- [ ] Create Shift entity.
- [ ] Create ShiftParticipant entity.
- [ ] Open shift with optional opening amount.
- [ ] Detect existing open shift.
- [ ] Resume shift without closing it.
- [ ] Record multiple participating cashiers.
- [ ] Register cashier activities against the shift.
- [ ] Close shift with optional actual cash.
- [ ] Calculate cash difference when actual amount is supplied.
- [ ] Support Not Reconciled status.
- [ ] Allow closing an old shift and opening a new one.
- [ ] Prevent simultaneous conflicting shift opening/closing operations.

## Phase 6 — POS & Sales

- [ ] Implement product search and barcode scanning.
- [ ] Implement retail/wholesale mode.
- [ ] Implement invoice cart.
- [ ] Implement quantity editing.
- [ ] Implement invoice-only price overrides.
- [ ] Implement discounts.
- [ ] Implement held invoices.
- [ ] Create sales invoices and invoice items.
- [ ] Associate sales with shift and performing cashier.
- [ ] Deduct stock on completed sales.

## Phase 7 — Payments & Printing

- [ ] Implement cash payments.
- [ ] Implement Visa/Card payment records.
- [ ] Implement InstaPay payment records.
- [ ] Implement wallet payment records.
- [ ] Calculate cash change.
- [ ] Separate cash and non-cash shift totals.
- [ ] Create printable sales receipts.
- [ ] Support Arabic receipt formatting.
- [ ] Support local thermal printer workflows.

## Phase 8 — Cash Outs & Returns

- [ ] Implement Cash Out with Amount and Reason.
- [ ] Associate cash-outs with shift and user.
- [ ] Deduct cash-outs from expected cash.
- [ ] Implement sales returns.
- [ ] Link returns to original invoices.
- [ ] Update stock and financial records.
- [ ] Record cash refunds against shifts.

## Phase 9 — Customers, Dashboard & Reports

- [ ] Implement Customer management.
- [ ] Implement Walk-in Customer.
- [ ] Implement dashboard summaries.
- [ ] Create sales reports.
- [ ] Create purchase reports.
- [ ] Create inventory reports.
- [ ] Create shift history and details.
- [ ] Show cashiers participating in each shift.
- [ ] Show sales by cashier.
- [ ] Show cash differences and unreconciled shifts.

## Phase 10 — Settings & Delivery

- [ ] Store profile.
- [ ] Arabic/English language settings.
- [ ] Tax and invoice settings.
- [ ] Printing settings.
- [ ] Local database backups.
- [ ] Database restore.
- [ ] Offline deployment.
- [ ] Functional and integration testing.
- [ ] Prepare installation instructions.

## Development Rules

1. Implement one phase at a time.
2. Use the existing three-tier structure.
3. Controllers never access DbContext directly.
4. All sensitive business rules are validated server-side.
5. Use database transactions for financial operations.
6. Use decimal for monetary values.
7. No internet-dependent runtime assets.
8. Do not add ASP.NET Core Identity.
9. Keep UI decisions separate from business requirements.
10. Update STATUS.md after verified progress.

## Definition of Done

A task is complete only after implementation, successful relevant verification, and documentation updates.

## Important Open Decisions

- Multiple physical drawers or one shared drawer.
- Mixed payment methods in one invoice.
- Return authorization policy.
- Negative stock policy.
- Exact tax calculation rules.
- Profit costing method.
- Backup frequency.
- Offline license mechanism.
