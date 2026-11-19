# StoresPOS — Master Development Plan

> **هذا الملف هو المصدر الأساسي للخطة.** أي تعديل على النطاق أو المراحل أو القرارات يتم هنا.  
> المرجع البصري/الوظيفي: [Claude Artifact](https://claude.ai/artifact/Gw9yNGvgsoa9WEGjYxZ8Eo) · الإصدار 1.0 · 18 نوفمبر 2026  
> تركيبة كل صفحة (مناطق/مكوّنات): [`docs/page-composition.md`](docs/page-composition.md)  
> التقدم الفعلي المُتحقَّق يُسجَّل في `STATUS.md` فقط.

---

## Project Decisions

- Framework: ASP.NET Core MVC + Razor
- Architecture: 3-Tier (PL → BLL → DAL)
- Database: SQL Server / SQL Server Express (local)
- Runtime: Local store device, offline (no internet-dependent assets)
- Currency: EGP, 2 decimal places
- Authentication: Custom JWT in HttpOnly cookie (`StoresPOS.AccessToken`)
- ASP.NET Core Identity: Not used
- Initial roles: Admin (مدير النظام) · Manager (مدير) · Cashier (كاشير)
- Shift model: One open shift, multiple participating cashiers
- Languages: Arabic (RTL) + English (LTR)
- Payments: Cash · Visa/Card · InstaPay · Vodafone Cash
- UI direction: Elegant / premium / modern / practical — green accent `#0F6751`, numbers first, keyboard-first POS

---

## Business Rules (locked)

1. One open shift for the whole store; second cashier joins as participant with per-cashier tracking.
2. Logout does **not** close the shift; close is an explicit authorized action.
3. Opening amount is optional (empty = 0).
4. Cash count on close is optional (empty = Not Reconciled — not treated as counted zero).
5. Only cash enters expected drawer totals; Visa / InstaPay / wallet stay in sales/reports only.
6. Price override applies to the invoice line only; catalog price unchanged; both prices + user are stored.
7. Cash-out = amount + reason; tied to open shift and user; reduces expected cash.
8. Markup % drives retail and wholesale sell prices (independent); new purchase cost recalculates both from saved markups.
9. Historical invoices keep the prices they were sold at.
10. Admin/Manager may use management screens without an open shift; shift is required for selling only.
11. A shift is "old" when it was opened before today's local store date — this only triggers the warning in journey C; it never auto-closes.
12. A cash refund is paid from the drawer of the **currently open** shift, not the shift of the original sale.

---

## Roles (default matrix)

| Permission | Admin | Manager | Cashier |
| --- | --- | --- | --- |
| POS · open/resume shift · cash-outs | ✓ | ✓ | ✓ |
| Close shift · price override · discounts · returns | ✓ | ✓ | Optional |
| Products · categories · purchases · inventory · reports | ✓ | ✓ | — |
| Users · settings · backup · license | ✓ | — | — |

Admin can toggle any permission per role later from Users & Roles.
Because of the "Optional" cells, authorization must be **permission-based** (policies per permission), not role-only `[Authorize(Roles=…)]`. The permission store + policies are built at the start of Phase 6; the Users & Roles screen comes later.

---

## System Modules (17)

Login · POS · Shifts · Cash-outs · Products · Categories · Inventory · Purchases · Sales invoices · Returns · Customers · Suppliers · Dashboard · Reports · Users & roles · Settings · Receipts

---

## User Journeys (test against these)

المصدر: قسم «رحلات المستخدم» في [`docs/client-proposal-ar.html`](docs/client-proposal-ar.html).  
كل رحلة تنتهي بحالة واضحة ومحفوظة.

| ID | الرحلة | الخطوات |
| --- | --- | --- |
| A | أول كاشير في اليوم | تسجيل الدخول → لا توجد وردية مفتوحة → فتح وردية (المبلغ اختياري) → البيع → إغلاق الوردية → العودة لتسجيل الدخول |
| B | انضمام كاشير ثانٍ | تسجيل الدخول → وردية مفتوحة بالفعل → استئناف → تسجيله كمشارك → نقطة البيع |
| C | وردية تُركت مفتوحة | تسجيل الدخول → ملخص الوردية القديمة → استئنافها أو إغلاقها → فتح وردية جديدة عند الإغلاق → نقطة البيع |
| D | بيع قطاعي | مسح / بحث → تعديل الكمية أو السعر → دفع · F9 → اختيار طريقة الدفع → حفظ + إيصال |
| E | مصروف نقدي | نقطة البيع أو صفحة الوردية → المبلغ + السبب → تحديث النقدية المتوقعة |
| F | شراء من مورد | فاتورة شراء جديدة → الأصناف + التكلفة → حفظ → زيادة المخزون → إعادة حساب الأسعار |
| G | مرتجع عميل | البحث عن الفاتورة → اختيار الأصناف والكمية → طريقة الاسترداد → تحديث المخزون والوردية |
| H | إغلاق الوردية | مراجعة الملخص → عدّ النقدية (اختياري) → تأكيد → طباعة التقرير → تسجيل الدخول |

---

## Design System (must follow)

- Tokens: primary `#0F6751`, primary-soft `#E7F4EF`, dark `#1A1D26`, bg `#F5F7FA`, surface `#FFFFFF`, text `#1F2937`, muted `#6B7280`, border `#E5E7EB`, warn `#D97706`, danger `#DC2626`, info `#2563EB`, neutral `#EEF0F3`
- Fonts (bundled offline): IBM Plex Sans Arabic · Manrope (EN headings) · Inter (EN body); tabular numerals
- Touch targets ≥ 44px; primary pay button 56px; target screens 1366×768+
- POS shortcuts: F2 search/scan · F4 customer · F6 discount · F7 resume held · F8 hold · F9 pay & print · +/−/Del qty

---

## Delivery Phases

Each phase is built, tested, and demoed before the next starts.

### Phase 1 — Foundation

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

### Phase 2 — Login & Roles

- [x] Create custom Users and Roles tables.
- [x] Create User and Role entities.
- [x] Implement secure password hashing using PBKDF2.
- [x] Create Login page (Username + Password + language hook).
- [x] Implement Login and Logout.
- [x] Implement JWT generation and validation.
- [x] Configure JWT Bearer Authentication (HttpOnly cookie).
- [x] Implement role-based authorization.
- [x] Create three initial roles: Admin, Manager, Cashier.
- [x] Create secure initial Admin seed.
- [x] Handle token expiration and unauthorized access.
- [x] Protect Controllers and sensitive endpoints.
- [x] Test authentication, authorization, expired tokens, and logout.

### Phase 3 — Products & Categories

- [x] Implement hierarchical categories (unlimited depth, reorder/move).
- [x] Implement Product entity and CRUD.
- [x] Arabic and English names.
- [x] Barcode and unit.
- [x] Purchase (cost) price.
- [x] Retail and wholesale selling prices.
- [x] Markup % calculations (edit price ↔ edit %; retail independent of wholesale).
- [x] Low-stock threshold per product.

### Phase 4 — Suppliers & Purchases

- [x] Supplier management (contact + purchase history totals).
- [x] Purchase invoice and line items.
- [x] Validate quantities and costs.
- [x] Increase inventory on confirmed purchase.
- [x] Recalculate product selling prices from saved markups.
- [x] Preserve purchase history (prices at time of purchase).

### Phase 5 — Inventory

- [x] Stock movement records.
- [x] Stock balance calculations.
- [x] Low-stock list / notifications.
- [x] Authorized stock adjustments.
- [x] Track user performing adjustments.

### Phase 6 — Shifts ✅

- [x] Permission-based authorization: `RolePermissions` table + seed from the default matrix + one policy per permission (e.g. `Shifts.Close`, `Sales.PriceOverride`, `Sales.Discount`, `Returns.Create`).
- [x] Localization groundwork: `IStringLocalizer` + resx (ar/en) for the layout/sidebar and all new Phase 6+ screens.
- [x] Shift entity + ShiftParticipant entity.
- [x] Enforce one open shift in the database (unique filtered index on open status) + transaction.
- [x] After login: Cashier → shift gate; Admin/Manager → Dashboard (POS link goes through the gate).
- [x] Open shift with optional opening amount.
- [x] Detect existing open shift.
- [x] Resume without closing; register participants.
- [x] Attribute cashier activities to the shift. (sales carry ShiftId + CashierUserId — Phase 7)
- [x] Close with optional actual cash; compute surplus/shortage when provided.
- [x] Support Not Reconciled status when count skipped.
- [x] Close old open shift then open a new one.
- [x] Prevent conflicting concurrent open/close operations.
- [x] If another cashier is mid-sale, wait until complete or held before close. (Phase 7: close blocked by non-empty Draft; closer can hold it)

### Phase 7 — POS & Sales ✅

- [x] Product search and barcode scanning.
- [x] Retail / wholesale mode toggle.
- [x] Invoice cart with quantity edit, persisted server-side as a `Draft` sales invoice per cashier (enables "wait for in-progress sale" on shift close and survives a browser close).
- [x] Invoice-only price overrides (permission-gated).
- [x] Discounts (permission-gated).
- [x] Hold / resume invoices.
- [x] Sales invoices + items; associate shift + cashier.
- [x] Deduct stock on completed sales.
- [x] Keyboard shortcuts (F2/F4/F6/F7/F8/F9). F4 shows a "Phase 10" notice until customers exist.

Defaults applied from Open Decisions (still to be confirmed by the client): one payment method per invoice; selling above stock blocked (no manager override yet); held invoices cancelled at shift close (warning shown); prices VAT-inclusive, no tax line until receipts.

### Phase 8 — Payments & Printing ✅

- [x] Cash payment + change calculation. (Phase 7 pay modal)
- [x] Visa/Card, InstaPay, Vodafone Cash payment records. (Phase 7)
- [x] Separate cash vs non-cash shift totals. (Phase 7 — shift summary + per cashier)
- [x] Thermal 80mm sales receipt (Arabic-capable; store language default).
- [x] Receipt fields: store header, invoice meta, lines at charged prices, discount, tender, change, barcode.
- [x] Local thermal printer workflow; reprint from sales list. (hidden-iframe print + Chrome `--kiosk-printing`, see `docs/printing.md`)
- [x] Shift close report printout. (auto-prints after close; X report from the current shift)

Store header, receipt language/digits and VAT line come from `appsettings.json` → `Store` until the Settings screen (Phase 11).

### Phase 9 — Cash-outs & Returns ✅

- [x] Cash-out: amount + reason; tie to shift + user; reduce expected cash. (POS header + Current shift modal; over-drawer = warning only)
- [x] Returns linked to original invoice (original prices; invoice discount shared pro rata; full return refunds exactly the invoice total).
- [x] Select lines/qty; refund method; restock + update shift cash when cash refund. (cash needs an open shift; non-cash only by the original method)
- [x] Cash-out and return lists with filters. (cashiers see their current shift only)
- [x] Find invoice by scanning the receipt barcode; return slip (80 mm) prints after saving.

Returns policy defaults: `Returns.Create` permission (Cashier OFF) + `Store:ReturnWindowDays` = 14. No in-line manager approval yet — a manager makes the return.

### Phase 10 — Customers, Dashboard & Reports ← next

- [ ] Customer management + default Walk-in ("عميل نقدي").
- [ ] Dashboard: today sales, cash vs non-cash, low stock, shift status, recent activity.
- [ ] Reports: sales, purchases, inventory, shifts (filters + print).
- [ ] Shift history: participants, sales by cashier, differences, unreconciled.

### Phase 11 — Settings & Delivery

- [ ] Store profile, language, tax, numbering, printing.
- [ ] Database backup / restore.
- [ ] Offline license activation.
- [ ] Offline installer / deployment.
- [ ] End-to-end functional testing.
- [ ] Installation instructions.

---

## Open Decisions (proposals — confirm or replace)

| Topic | Proposal |
| --- | --- |
| Cash drawers | One shared drawer (matches one-shift model) |
| Split tender | Not in v1; add later if needed |
| Held invoices | Survive until shift close; then cancel/resume list |
| Returns policy | Manager approval; within 14 days |
| Sell at zero stock | Block by default; Manager override |
| Tax | Prices inclusive of 14% VAT; show on receipt |
| Profit costing | Weighted average cost |
| Backup | Daily auto + on every shift close; second disk/USB |
| License | Offline device-bound license file we issue |
| Numerals | Western digits on screen; receipt digits configurable |
| Dark mode | Proposal promises it "for evening shifts"; tokens in `theme.css` make it cheap — confirm keep (Phase 11) or remove from proposal |
| Held invoices at close | Close screen lists held invoices: cancel all, or carry over to the next shift |

**Must be confirmed by the client before Phase 7** (they change POS directly): split tender · held invoices · sell at zero stock · tax.

---

## Development Rules

1. Implement **one phase at a time**.
2. Keep the three-tier structure; Controllers never access `DbContext` directly.
3. Validate all sensitive business rules server-side.
4. Use database transactions for financial operations.
5. Use `decimal` for money; EGP with 2 decimals.
6. No internet-dependent runtime assets (fonts, CSS, JS bundled).
7. Do **not** add ASP.NET Core Identity.
8. Follow the design system tokens above for PL UI work.
9. Screen states for every screen: normal · empty · loading · error · confirm.
10. Update `STATUS.md` only after verified progress (build/tests/demo notes).

---

## Definition of Done

A phase task is done only after: implementation + relevant verification (build/tests) + `STATUS.md` update.
