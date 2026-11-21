# STATUS.md — StoresPOS Implementation Status

## Project Status

- Project name: StoresPOS
- Master plan: `plan.md` (source of truth)
- Current phase: **Phase 24 complete** — print template designer (improvement plan `docs/improvement-plan.md`, phases 17–25)
- Overall progress: Phases 1–24 verified
- Latest verified build result: Succeeded
- Last update date: 2026-11-21

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

### Phase 9 — Cash-outs & returns

- `CashOut` (shift, user, amount, reason) — `ICashOutService`: amount > 0 + reason required, user must be in the open shift, more than the drawer = warning (not blocked)
- `SalesReturn` / `SalesReturnItem` (`R-000001`, original invoice, shift, user, refund method, reason, line snapshots)
- `IReturnService`: find invoice by number (scanned barcode or bare sequence), returnable quantities per line, refund at charged price × (Total/Subtotal) so the invoice discount is shared; the return that empties an invoice refunds exactly what is left; `Returns.Create` checked in DB; 14-day window (`Store:ReturnWindowDays`, 0 = off); cash refund needs an open shift, non-cash only by the original method (recorded, not in drawer)
- Stock back via `SaleReturn` movements (`IInventoryService.ApplySaleReturnAsync`)
- Shift totals: cash refunds + cash-outs lower expected cash; non-cash refunds shown separately; cash-outs per participant
- PL: cash-out modal (`_CashOutModal` + `cashout.js`) on POS header and `Shifts/Current`; `CashOuts/Index`; `Returns/Create` (scan → lines → method → reason), `Returns/Index`; `Receipts/Return/{id}` 80 mm slip auto-prints after saving; Sales list "partly/fully returned" chips; Sales details lists returns + "Start return"; sidebar links; POS "Return" button (with permission)
- List visibility: Reports.View sees all; otherwise the current shift only (policies `CashOuts.View`, `Returns.View`)
- Arabic strings for all Phase 9 screens and messages
- Migration `CashOutsAndReturns`
- Tests: BLL 73/73, PL 10/10

### Phase 10 — Customers, dashboard, reports, shift history

- `Customer` (name, mobile, address, notes, default sale type, active). Walk-in ("عميل نقدي") = invoice with no customer, so it can never be edited or deleted
- `ICustomerService`: list/search (name or mobile), create/update/deactivate; a mobile number belongs to one active customer; totals = completed sales − returns
- POS F4: picker modal (search, ↑/↓/Enter, walk-in button, quick add name + mobile). Picking a customer switches the invoice to their default sale type (edited prices kept); Clear and a new invoice go back to walk-in; held invoices keep their customer
- Customer shown on the 80 mm receipt, Sales list/details, held list, shift details; `Customers/*` screens with purchase history + returns
- Dashboard (`Home/Index`): today's sales / cash / non-cash / returns (store-local day), shift status + expected drawer, low-stock top 5, latest sales / cash-outs / confirmed purchases
- `Reports/*` (Reports.View): Sales (net of discount — each line carries its share; returns dated by refund day; cost = purchase price saved on the line; by method / cashier / sale type / day / top products; filters cashier, method, sale type, category incl. sub-categories), Purchases (confirmed, by invoice date; by supplier / product), Inventory (value at cost + retail, low/out, movements in range), Shifts (shortage / surplus / unreconciled, sales by cashier), Cash-outs (by user, by reason). Default range = this month; browser print hides the shell
- `Shifts/Index` history (filters dates / result / cashier) + `Shifts/Details/{id}` (drawer summary, by cashier, cash-outs, returns, invoices, reprint report); links from Current shift and the gate
- Sidebar: Customers + Reports enabled for Reports.View
- Arabic strings for all Phase 10 screens and messages
- Migration `Customers` (applied to the Mac dev SQL — Azure SQL Edge)
- Tests: BLL 86/86, PL 12/12

### Phase 11 — Payment methods & A4 invoice

- `PaymentMethods` table (`PaymentMethodDefinition`): NameEn/Ar, Kind (Cash/Card/Wallet/BankTransfer/Cheque), IsActive, SortOrder, optional DefaultTreasuryId (Phase 12)
- Seed keeps ids 1–4 + Cheque=5 so historical invoice/return bytes stay valid; `PaymentMethod` enum kept as id constants
- Drawer rule uses Kind=Cash (not hard-coded id); sales/returns/shift/dashboard/reports wired through `IPaymentMethodService`
- Permission `PaymentMethods.Manage` (Admin on; Manager optional/off); CRUD + reorder screen under Settings in the sidebar
- POS pay modal, returns refund radios, Sales/Reports filters: active methods from DB (`data-cash` in pos.js)
- A4 landscape: `Receipts/SaleA4`, `ReturnA4`, `PurchaseA4` + amount-in-words (ar/en); Sales list/details print 80 mm or A4
- Migration `PaymentMethods` applied
- Tests: BLL 89/89 (incl. payment-method seed/inactive/create), PL 12/12
- **Re-login required** after deploy so Admin JWT picks up `PaymentMethods.Manage`

### Phase 12 — Treasuries (الخزائن)

- `Treasury` + `TreasuryTransaction` ledger (In/Out, source type + id, user, note, transfer group); balance = opening + ledger (rule 16)
- Seed: Main cash (default) + Main bank; deactivate allowed, no silent balance edits
- `ITreasuryService`: CRUD, deposit/withdraw (reason required), transfer (two ledger rows / one TX), statement with date filters + running balance
- Shift close: required handover treasury; amount = counted cash or expected; shift report shows handover; `Shift.HandoverTreasuryId` / `HandoverAmount`
- Non-cash sales (except Cheque) settle into the method's `DefaultTreasuryId` when set; payment-method edit has treasury picker
- Purchases: `PaidFromTreasuryId` required at confirm → treasury Out
- PL: `Treasuries/*` (list, edit, deposit, withdraw, transfer, statement + print); dashboard balances card; sidebar link
- Permissions `Treasuries.View` / `Move` / `Manage` (Admin full; Manager View+Move; Manage optional/off)
- Migration `Treasuries` applied
- Tests: BLL 93/93, PL 12/12
- **Re-login required** after deploy so JWT picks up treasury permissions

### Phase 13 — Cheques (الشيكات)

- `Cheque` + `ChequeStatusHistory`: number, bank, due date, amount, In/Out, customer XOR supplier, optional sale/purchase link; status log with user + treasury
- Incoming: Pending → Collected (treasury In) / Bounced / Cancelled; outgoing: Pending → Paid (treasury Out) / Cancelled — no money on bounce/cancel (rule 15)
- Manual add from Cheques screen; customer/supplier pages; POS Cheque pay (needs customer + number + due date); purchase confirm pay-by-cheque
- Cheques UI: لينا / علينا tabs, filters, totals, print; collect/pay/bounce/cancel actions
- Dashboard due today / week / overdue (in + out); Reports → Cheques; A4 shows cheque number
- Permissions `Cheques.View` / `Manage` / `ChangeStatus` (Admin + Manager)
- Migration `Cheques` applied
- Tests: BLL 97/97, PL 12/12
- **Re-login required** after deploy so JWT picks up cheque permissions

### Phase 14 — Warehouses (المخازن)

- `Warehouse` + `WarehouseStock`; seed Main warehouse; migrate `Product.StockQuantity` into Main; `Product.StockQuantity` kept as denormalized total
- Every `StockMovement` has `WarehouseId` (before/after = warehouse qty); `StockMovementType.Transfer`
- `StockTransfer` / `StockTransferItem` document (rule 17 / journey M) + list + print
- Purchases: `ReceivedIntoWarehouseId` at confirm; POS/returns use `Store:SellingWarehouseId` (0 = default)
- Inventory balances / movements / adjust + inventory report filter by warehouse
- Permissions `Warehouses.View` / `Transfer` / `Manage` (Admin all; Manager View+Transfer; Manage optional/off)
- Migration `Warehouses` applied
- Tests: BLL 101/101, PL 12/12
- **Re-login required** after deploy so JWT picks up warehouse permissions

### Phase 15 — Settings & Delivery

- Settings screen (Admin): store profile · sales/tax · numbering · printing · language · backup · license; persists to `App_Data/store-settings.json`
- Selling warehouse, default treasury, default receipt 80 mm/A4, logo upload for A4, allow-sell-at-zero toggle
- Backup now / list / restore (SQL `.bak`); backup on shift close + daily hosted job
- Offline license: device-bound HMAC file; `tools/issue-license.ps1` to issue
- Docs: `docs/installation.md`, `docs/deployment.md`, `docs/e2e-checklist.md`; printing.md updated
- Tests: BLL 103/103, PL 12/12

### Phase 16 — Printing studio, Appearance, Owner & Users

- Settings → Printing: Windows printer list/default/refresh, live 80 mm + A4 sample preview, layout toggles + font scale; `DefaultPrinterName` synced to OS default when possible
- Settings → Appearance: light/dark, primary/sidebar colors, UI logo, favicon, store name in shell + login
- Role `Owner`; first Admin promoted on upgrade; `UserPermissions` overrides role matrix for JWT + `PermissionService`
- Users & Roles UI: CRUD, per-user tab checkboxes, Manager/Cashier role defaults editor
- POS Reprint last respects `DefaultReceiptFormat`; migration `OwnerAndUserPermissions`
- Docs: `docs/printing.md` updated; **re-login** after tab/permission changes

### Phase 17 — Safety net & measurement (improvement plan)

- Plan saved as `docs/improvement-plan.md` (phases 17–25)
- New test `tests/StoresPOS.PL.Tests/LocalizationCoverageTests.cs`: scans `L["…"]` / `_l["…"]` in PL views + C#, `[Display(Name = …)]` and `ErrorMessage = …` in PL, BLL `const string Msg… = "…"` and literal `new BusinessException("…")` (BLL + PL); fails listing every key missing from `SharedResource.ar.resx`. Also checks the resx has no empty values
- First run found **172 missing keys** (the plan's ~10 — Sort, English, Arabic, Kind, Reorder, Save order, Created by, Confirmed by, Unit, VAT included — plus 93 BLL messages, 66 view-model labels/validation texts, and 3 upload errors in `SettingsController`); all added with Arabic text → test green
- Not caught by the scan (by design): texts built at runtime or chosen with a ternary inside `new BusinessException(cond ? "a" : "b")` — the 3 in Settings were added by hand
- Settings timing: `SettingsController.BuildPageAsync` logs each stage (`Settings page timing (tab …): settings / warehouses / treasuries / backups / license / printers / total ms`). First measurement on the dev PC (2026-11-21): **printers 67 755 ms on the first open** after app start (Windows printer enumeration), then ~50 ms; everything else < 30 ms. Input for Phase 18
- Product names in cashier stock messages (out of stock / not enough / inactive) use the Arabic name in the Arabic UI (`SalesService.MessageName`, text only — done in the toast round, see change log)
- No migration in this phase. Sale screen (pos.css / pos.js / Pos views) not touched. No CDN assets
- Repository note: the working tree has many uncommitted changes from the POS redesign rounds, so no phase branch / commits were made (rule in plan §9 not applied) — backups kept as folders (`_pos_redesign_backup*`, `_phase17_backup`)
- Defaults from plan §9 used: none needed in this phase (decisions only apply from Phase 18 on)
- Tests: BLL 108/108; PL 13/14 — coverage tests 2/2 green; known failure `PosJourneyTests.Scan_Pay_DeductsStock_AndShowsInSalesList` (this PC's `App_Data/store-settings.json` prints Arabic-Indic receipt digits, test expects `200.00`)
### Phase 18 — Settings: speed and layout

- Measured before: printing tab **67 755 ms** on the first open (Windows printer list), every tab loaded every tab's data. After: printing tab **31 ms** server time (log `Settings page timing (tab printing): … total 31 ms`), printers arrive by AJAX
- Each tab loads only its own data (`SettingsController.BuildPageAsync` switch): store → warehouses + treasuries, backup → backup files, license → license; nothing else
- `WindowsPrinterService`: list + Windows default read together once and cached 60 s; an expired list is served while it refreshes in the background; "Refresh" (`/Settings/Printers?refresh=true`) asks Windows again; `PrinterWarmupHostedService` reads it once at start-up
- One receipt preview (80 mm | A4 switch), loaded only when visible, 600 ms debounce, a new load cancels the old one
- New layout: grouped side navigation (Store · Appearance and language · Printing · Data), sticky save bar with "Unsaved changes", Discard, leave warning, double-submit guard, settings search (opens the right tab and highlights the field)
- View split into partials (`Views/Settings/_Store`, `_Sales`, `_Numbering`, `_Appearance`, `_Language`, `_Printing`, `_Backup`, `_BackupList`, `_License`); script moved to `wwwroot/js/settings.js`
- Tests: `SettingsJourneyTests` (every tab opens, printers by AJAX, Arabic)

### Phase 19 — Default printers per paper size + shift report A4/80 mm

- `StoreSettings`: `ThermalPrinterName`, `A4PrinterName`, and per document (`SalePrint`, `ReturnPrint`, `PurchasePrint`, `ShiftPrint`): paper size, print automatically, copies (1–5, silent printing only)
- **Older settings keep working with no change**: empty values fall back at read time (`FormatFor`: sale/return → old `DefaultReceiptFormat`, purchase → A4, shift → 80 mm; `PrinterFor`: format printer → old `DefaultPrinterName` → Windows default)
- One settings mapper (`BLL/Models/SettingsMapper`, JSON by property name) replaces the six hand-written copies (service clone + JSON writer, controller ToForm/ToDto, receipts clone); saving a tab copies the fields marked `[SettingsTab("…")]` (replaces the `MergeTab` switch)
- `SilentPrintService.ResolvePrinterName(settings, format, windowsDefault)`; the test button prints to the printer chosen next to it
- `Receipts/ShiftA4.cshtml` + action `ShiftA4` + `format` parameter on `Receipts/Shift` (default = Shift report setting)
- Shift screens (`Shifts/Closed`, `Details`, `Current` X report): "80 mm | A4" switch, starts on the setting, remembers the user's last choice (browser), auto-print at close uses it; "print automatically" off = no auto-print at close / after a return / in POS (POS choice starts on "none")
- Settings → Printing: printers section (thermal, A4, fallback, each with "Test print") + "Document defaults" table
- Not done: stock transfer has no printed document yet, so it has no row
- Tests: `PrintSettingsTests` (printer per format, invoice A4 + return 80 mm, old settings, mapper round trip, tab merge), shift A4 content in `ShiftJourneyTests`, BLL save test

### Phase 20 — Shared tables, list toolbar, paging/sort/search, forms

- `ListQuery` (page, pageSize 25/50/100, sort, dir, q) + `PagedResult<T>` + `ToPagedAsync` (BLL `DTOs/PagingDtos.cs`): sort only on listed columns, count + one page on the database, optional total over all pages
- Server paging on 9 lists: sales, products, stock movements, customers, suppliers, purchases, returns, cash-outs, cheques (cheque totals still cover every matching cheque). Existing `GetListAsync` callers unchanged
- Shared partials: `_ListToolbar` (search `q`, removable filter chips, result count, Export CSV), `_SortTh`, `_Pager`; CSV export of every matching row (UTF-8 BOM for Arabic in Excel, formula-injection safe), `?export=csv`
- `.sp-table` everywhere: all 55 screen tables (5 class variants before); sticky header, tabular numbers, hover, footer totals, compact variant. Only A4/receipt print tables differ (print CSS)
- `.sp-form` system + `_FormActions` sticky save bar (unsaved state, Cancel, Save), required marks, inline errors, numeric keyboards, leave warning, double-submit guard. Applied to: products, purchases, customers, suppliers, treasuries, cheques, warehouses, payment methods (users in Phase 21)
- Old `search=` links still work (alias of `q`)
- Tests: `PagingTests` (incl. 50 000 products → one page, sorted and searched on the server), `ListJourneyTests` (page/sort/search/CSV on customers, toolbar + CSV on all 9 lists, grep rule: no table without `.sp-table`)

### Phase 21 — Users and permissions

- Migration **`UserSecurityAndAudit`**: `Users` + `PermissionStamp`, `SecurityStamp`, `FailedLoginCount`, `LockedUntilUtc`, `MustChangePassword`, `ApprovalPinHash/Salt`; new table `AuditLogs`
- **No new sign-in needed** for permission changes: tokens carry `pstamp`/`sstamp`; `UserStampMiddleware` (30 s cache, cleared by the Users screens) re-issues the cookie silently when permissions changed and drops it when the security stamp changed (sign out everywhere, password reset, deactivation). Tokens from before this phase are upgraded silently on the next request
- Lockout: 5 wrong passwords → locked 15 minutes (audited); admin can unlock
- Users list: search, role/status filters, initials avatar, custom-permissions / locked / must-change badges, last sign-in, row menu (edit, permissions, reset password, sign out everywhere, activate/deactivate)
- User page tabs: Details · Role and permissions · Security (reset password with "must change", approval PIN 4–6 digits, unlock, sign out everywhere) · Activity (audit log)
- Permission matrix grouped Sales / Inventory / Finance / Reports / System (`AppPermissions.Groups`), search, select all per group, "from role" vs "custom" per item, back to role per item and for all, live preview of the sidebar the user will see. Same set as the role = stored as "use role"
- Roles page: one roles × permissions matrix (Owner/Admin locked on), saves all roles at once; templates Cashier / Manager / Accountant / Storekeeper → new role
- Guards: last active Owner cannot be removed, nobody can deactivate themself or remove their own Users access, lockout. Audit for user/role/permission/password/PIN/sign-out changes (never the password itself)
- Change password page (`/Account/ChangePassword`, link in the sidebar); required first when an admin reset it with "must change"
- The PIN is stored and managed; using it to approve actions at the till is not wired yet (returns approval is still an open client decision)
- Tests: `UserSecurityTests` (BLL), `UserPermissionsJourneyTests` (PL: permission applies on the next request, sign out everywhere, must-change flow, pages in en/ar)

### Phase 22 — Dashboard

- Built from the approved mockup `docs/dashboard-mockup.html` (approved 2026-11-21)
- Periods: Today / Yesterday / 7 days / 30 days / This month (`?period=`), each compared with the same-length period before (month: same days of the previous month)
- First paint: greeting + date + period buttons + shift chip, alerts only when needed (old open shift, cheques overdue / due today, out of stock, backup older than 24 h, license ends ≤ 14 days — the last two for Settings.Manage), 5 KPI cards (net sales = sales − returns, invoices, average invoice, cash in drawer, returns) with change arrow and inline-SVG sparkline
- After the page: `Home/Charts` (JSON → Chart.js: net sales by hour for one day / by day otherwise, current vs previous; payment-method doughnut) and `Home/Details` (partial: top 5 products, sales per cashier, running low with "Purchase order" → `Purchases/Create?productId=` pre-filled, treasury balances, cheques due in 7 days, one merged activity timeline)
- Cache per period, 45 s (`Dashboard:CacheSeconds`, 0 in tests). `DashboardService`: `GetRange`, `GetOverviewAsync`, `GetChartsAsync`, `GetDetailsAsync` (old `GetAsync` kept)
- Chart.js 4.4.1 (MIT) served locally from `wwwroot/lib/chartjs/dist/chart.umd.js`, recorded in `libman.json` — no CDN at runtime. RTL: time axis right-to-left, values on the right
- No migration. Profit card still waits for the profit-costing decision (plan §9)
- Checked in the browser on the dev database (7 days: net 8,871.00 = 8,878.00 sales − 7.00 returns), English and Arabic
- Tests: `DashboardPeriods_…` (BLL: ranges, comparison, hour/day buckets, payment split, top products, cashiers, activity), `DashboardJourneyTests` (PL: every period, charts JSON, details partial, cashier blocked, Arabic); Phase 10 dashboard checks updated to the new page

### Phase 23 — Reports

- One report shell: period presets (Today, Yesterday, 7 days, This month, Last month, This year) + filters, summary cards, one chart (`_ReportChart` → `wwwroot/js/reports.js`, local Chart.js), tables, **Excel / CSV / A4 print** on every report; A4 print header carries the store name, address, phone and tax number
- Export: `PL/Reports/ReportExport` (ClosedXML 0.104.2, MIT — added to StoresPOS.PL): title, period, summary, one sheet per table, totals row, auto-filter, frozen header, RTL sheet in Arabic; CSV = same content, UTF-8 BOM, formula-safe. Built from the same DTO as the screen (test: Excel net sales = screen)
- Comparison: sales report compares every card with the same-length previous period (▲/▼ %) and charts this period vs the previous one
- Drill-down: cashier / product / day → sales list filtered (new `cashierId` and `productId` filters on `/Sales`); people in discounts/drawer reports → their invoices / shifts; price-change lines → the invoice
- New reports: **Discounts and price changes** (discounts, price changes vs catalog, cancelled carts, returns per person + every price-change line), **Busy hours** (by hour, weekday, hour × weekday heat table), **Product performance** (best/worst sellers, stock not sold for 30–365 days with value at cost), **Profit** (by product, category, day), **VAT** (VAT in sales and returns per day at the Settings rate), **Drawer differences** (shortage/surplus per person; a shift counts for everyone who worked it), **Cheque aging** (pending cheques by not due / 1–30 / 31–60 / 61–90 / 90+ days late)
- **Profit costing (plan §9 default applied: weighted average)**: migration **`ProductAverageCost`** adds `Products.AverageCost` (decimal 18,4; filled from `PurchasePrice`). Each purchase receipt updates it ((on hand × average + received × cost) ÷ total); a sale saves it as the line cost. Sell prices still follow the last purchase price (unchanged). Sales made before this keep the cost they were saved with
- **New permission `Profit.View`** (Owner/Admin by default, plan §9): profit/cost/margin hidden from the sales report, its Excel and the Profit tab without it. Added to running stores at start-up with a permission-stamp bump, so the Owner gets it without signing in again
- Customer / supplier statements: **not built** — the system has no credit (آجل) balances (every sale and purchase is settled at the time), so there is nothing to put on a statement yet. Revisit if credit sales are added
- VAT uses today's rate (invoices do not store their rate); stated on the report
- Tests: BLL — discounts audit, hours/weekday, profit (net of discount and returns), VAT, product performance + slow stock, drawer, cheque aging, weighted average (unit + purchase receipt); PL — all 13 reports open + Excel + CSV, Excel = screen, drill-down, Profit.View hidden/forbidden, Arabic

### Phase 24 — Print template designer

- Plan §9 default applied: **sections you switch on/off and reorder** (no free template language). One template per document and paper size: sales invoice 80 mm / A4, return slip 80 mm / A4, purchase invoice A4, shift report 80 mm / A4
- Template = section order + visible sections (header · document details · items · totals · footer); header switches (logo, store name, address, phone, tax no., commercial register) + extra line ar/en; which details, item columns and totals show; item names (receipt language / Arabic / English / both); amount in words; footer (thank-you text, return policy ar/en, bank details ar/en, signature boxes, barcode, document number); print (font size, line spacing, A4 margins and orientation)
- Stored in `App_Data/print-templates.json` (`IPrintTemplateService`, BLL). **No change to existing printouts**: until a template is saved, the default copies the earlier layout and the older receipt switches (ShowLogoOn80mm, ShowAddressOnReceipt, … stay in settings as those defaults). No migration
- **One document model for both print engines** (plan §9: keep Razor + System.Drawing until tested on a thermal printer): `PrintDocumentBuilder` builds a `PrintedDocument` (labels in the receipt language, template already applied); `Views/Receipts/Document80` / `DocumentA4` draw it on the page and `SilentPrintService.BuildLines` draws the same document for Windows silent printing — a template change reaches both. The seven old print views moved to `_phase24_backup/Views/Receipts` (two had uncommitted edits)
- Designer: Settings → Printing → **Print templates** — document/size picker, sections with ↑/↓ and on/off, all switches and texts, live preview of the unsaved template on sample data (POST `Receipts/TemplatePreview`; A4 drawn at real size and scaled), Save, Reset to default, unsaved-changes warning. The Printers tab's old look switches are replaced by a link to the designer
- Not done: stock transfers still have no printed document (no template); logo and barcode images are not drawn by silent printing (text only, as before)
- Tests: BLL `PrintTemplateTests` (defaults from old settings, save/read/reset per document and size, clean-up, unknown size refused); PL `PrintTemplateJourneyTests` (hidden columns/details/totals, silent-print lines follow the same order and switches, designer opens for all 7, preview, save → receipt changes, other size untouched, reset); existing receipt/shift report tests still green

### Phase 25 — Final translation pass

- **Every page checked in both languages by a test** (`ArabicPageScanTests`): signs in as admin and opens ~60 screens (lists, forms, details, all Settings tabs, all 13 reports, receipts, shift gate, login) in Arabic and fails on any English word left in what the user reads (text, `<title>`, placeholders, titles, aria-labels, script messages in `data-*` attributes); in English it fails on Arabic UI translations that leaked in. Brand names, units, codes and seeded people/roles are allowed. Done on the rendered HTML in-process — **no Playwright/browser download** (plan §9: no new internet assets); script-built text is covered because scripts take their messages from translated `data-msg-*` attributes
- `LanguageSwitchTests`: switch to Arabic on the login page → sign in → pages stay RTL → open a shift → the POS has no English → switch back to English from the POS
- Wrapped the remaining hard-coded English in Categories, Inventory, Products, Purchases, Suppliers and the catalog tabs with `L[...]` (originals in `_phase25_backup/Views/`); empty states, Active/Inactive, adjust example, product/purchase form hints
- Names of records follow the UI language (`Ui.Name(en, ar)` in `PL/Extensions/Ui.cs`): categories, products, warehouses, treasuries, payment methods, cheques, purchases, report filters. Lists that show both names mark the second one with `lang` and show the other language's name (Products, Inventory, Suppliers, purchase lines showed the Arabic name twice in Arabic — fixed)
- Enum values (cheque status/direction, payment kind, treasury type, movement type) translated on screen and in dropdowns (`Ui.EnumItems`)
- **Error messages translated**: services throw keyed messages with arguments (`"Stock for '{0}' cannot go below zero (would be {1})."`, …; product names passed as `DisplayName` so they follow the UI language); controllers show them with `this.Localizer().Localize(ex)` (29 places). License status messages translated
- **Login page rendered by the server** in the chosen language (dir/lang, RTL Bootstrap); its switch posts to `/Culture/Set` like the rest of the app, so the choice sticks through sign-in. The old client-side `wwwroot/js/login.js` moved to `_phase25_backup/login.js`
- Resource clean-up: keys differing only by case (`Today`/`today`, `— None —`/`— none —`, …) clashed in lookups; duplicates removed and views use one spelling. ~100 keys added
- pos.js not touched (plan §9); the POS view now passes the two cheque messages as `data-msg-*`
- Not done: printed documents still use the store's receipt language on purpose (Settings), not the screen language

### Test host isolation (found while running the tests)

- Tests used this PC's `App_Data/store-settings.json` and real Windows printers → `PosJourneyTests.Scan_Pay` failed here and the printer list made runs slow. `Program.cs` accepts an optional `AppDataPath` setting; the test host uses its own temp folder and a fake printer service. PL suite: ~60 s → ~6 s, all green

## Pending Tasks
- Returns: no in-line manager approval for cashiers yet (manager makes the return) — confirm with client
- Silent printing on the real thermal printer not tested (no printer here) — follow `docs/printing.md` on the store PC; include a customized print template in that test
- Run `docs/e2e-checklist.md` on a store PC; apply migrations through latest on every store SQL
- Profit costing: weighted average applied (plan §9 default, Phase 23) — client to confirm
- Customers screens use Reports.View (Admin/Manager); cashiers add customers only from the POS (F4) — confirm with client
- Minus signs in the existing `_ShiftSummary` render after the number in Arabic (e.g. "0.00-") — cosmetic
- Client to confirm the POS open decisions applied as defaults (split tender, zero stock, held at close, tax)
- Approval PIN (Phase 21) is stored but not yet asked for at the till
- Per-document "copies" only applies to silent printing; browser printing prints one copy
- Defaults from plan §9 used: printers for the whole store (not per PC); permission level stays per tab (no view/edit/delete grades); Chart.js (Phase 22) and ClosedXML (Phase 23) in use; profit = weighted average cost; profit visible with Profit.View (Owner/Admin)
- Non-cash settlement into default treasury — confirm with client (applied as default)

## Tests & Build Results

| Command | Result |
| --- | --- |
| `dotnet build StoresPOS.sln` | Succeeded |
| `dotnet test tests/StoresPOS.BLL.Tests` | Passed 134/134 |
| `dotnet test tests/StoresPOS.PL.Tests` | Passed 70/70 |
| `dotnet ef database update` | Applied through `ProductAverageCost` (dev DB, at app start) |

## Next Recommended Task

Owner review of phases 18–25 and commit. Test silent printing of a customized template on the store's real thermal printer. Apply migrations `UserSecurityAndAudit` and `ProductAverageCost` on every store SQL.

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
| 2026-11-19 | Client request: plan gained phases 11–14 (payment methods + A4 invoice, treasuries, cheques, warehouses); settings & delivery → Phase 15 |
| 2026-11-19 | Phase 10 customers (POS F4), dashboard, reports, shift history |
| 2026-11-19 | Phase 9 cash-outs, returns, return slip, lists |
| 2026-11-19 | Phase 8 receipts, shift report, printing workflow |
| 2026-11-19 | Phase 7 POS, sales invoices, shift totals + close guard |
| 2026-11-19 | Phase 6 shifts + permissions + ar/en localization groundwork |
| 2026-11-19 | `page-composition.md` rewritten as per-page build spec; `plan.md` gained rules 11–12, permission/localization/Draft-invoice tasks, new open decisions; sidebar reordered (+Shifts, +Cash-outs, Configuration→Settings) — build verified, BLL tests 29/29 |
| 2026-11-19 | UI parity pass: tokens, shell, shared components, Inventory→Products→rest; RTL logical props |
| 2026-11-19 | Phase 11 payment methods table (+ Cheque) + manage screen + A4 landscape invoices |
| 2026-11-19 | Phase 12 treasuries ledger, shift handover, purchase pay-from, statement + dashboard |
| 2026-11-19 | Phase 13 cheques (لينا/علينا), collect/pay → treasury, POS + purchase + reports |
| 2026-11-19 | Phase 14 warehouses, stock transfers, per-warehouse inventory + POS selling warehouse |
| 2026-11-19 | Phase 15 settings UI, backup/restore, offline license, install/deploy docs + E2E checklist |
| 2026-11-20 | Phase 16 printing studio (printers + preview), Appearance tab, Owner role + Users & Roles |
| 2026-11-20 | POS redesign rounds (UI): Poppins/Plex fonts, customer bar, cash modal, print choice, toasts; customer type (migration `CustomerType`: new `Customers.CustomerType` tinyint default 1, back-filled from `DefaultSaleType`) |
| 2026-11-21 | Phase 17: `docs/improvement-plan.md`, translation coverage test (+172 Arabic keys), settings page timing log |
| 2026-11-21 | Phase 18 settings speed + layout (printing tab 67 s → 31 ms), Phase 19 printers per paper size + shift A4, Phase 20 shared tables/paging/CSV/forms, Phase 21 users & permissions (migration `UserSecurityAndAudit`), Phase 22 dashboard mockup; test host isolated from this PC's settings/printers |
| 2026-11-21 | Phase 22 dashboard built (periods, comparison, Chart.js local, after-load partials, 45 s cache, purchase order from running low) |
| 2026-11-21 | Phase 23 reports: shell + presets + charts, Excel/CSV (ClosedXML), comparison, drill-down, 7 new reports, weighted average cost (migration `ProductAverageCost`), `Profit.View` |
| 2026-11-21 | Phase 24 print template designer: templates per document and size, one document model for page + silent print, live preview |
| 2026-11-21 | Phase 25 translation pass: every page scanned in ar/en by tests, remaining views wrapped, names/enums/errors follow the UI language, server-rendered login switch |

## Local Dev Login

- Username: `admin` (seeded as **Owner** after Phase 16 upgrade)
- Password: `Admin@12345`
- URL: http://127.0.0.1:5188
- After changing a user’s tabs: they must **sign in again** (JWT)
