# StoresPOS — Project Overview

## 1. What Is This Project?

**StoresPOS** is a local, offline-first **Point of Sale (POS)** and **Store Management** system for small and medium retail stores.

It is a desktop/local Windows web application built with **ASP.NET Core MVC**. The store can run sales, inventory, purchases, shifts, and reports **without internet**.

In short:

| Question                 | Answer                                                  |
| ------------------------ | ------------------------------------------------------- |
| What is it?              | A complete cashier + store back-office system           |
| Who is it for?           | Store owners, managers, and cashiers                    |
| Where does it run?       | Local Windows PC / local SQL Server                     |
| How is it used?          | Browser UI (Razor MVC) talking to a local database      |
| What makes it different? | Offline-ready, simple daily workflows, Arabic + English |

---

## 2. What Does The Project Do?

StoresPOS covers the full daily store loop:

1. **Sign in** with role-based access (Admin / Manager / Cashier).
2. **Open or resume a shared shift** for the till.
3. **Sell products** from the POS screen (barcode, search, cart, discounts).
4. **Accept payments** (Cash, Card/Visa, InstaPay, Wallet).
5. **Print receipts** locally.
6. **Buy stock** from suppliers and update inventory automatically.
7. **Track returns, cash-outs, customers, and reports**.
8. **Configure** store profile, language, tax, printing, backup.

### Core business outcomes

- Faster checkout with fewer clicks.
- Clear cash drawer expectations per shift.
- Accurate stock after sales and purchases.
- Historical invoice prices preserved.
- One open shift shared by multiple cashiers.

---

## 3. What Is Inside The Solution?

```text
StoresPOS/
├── StoresPOS.sln
├── plan.md                 # Master roadmap (phases & tasks)
├── STATUS.md               # Actual verified progress
├── docs/
│   └── projectOverview.md  # This file
├── src/
│   ├── StoresPOS.PL/       # Presentation Layer (MVC UI)
│   ├── StoresPOS.BLL/      # Business Logic Layer
│   └── StoresPOS.DAL/      # Data Access Layer
└── tests/
    ├── StoresPOS.BLL.Tests/
    └── StoresPOS.DAL.Tests/
```

### Layer responsibilities

| Layer   | Project         | Responsibility                                                   |
| ------- | --------------- | ---------------------------------------------------------------- |
| **PL**  | `StoresPOS.PL`  | Controllers, Razor Views, ViewModels, auth UI, static assets     |
| **BLL** | `StoresPOS.BLL` | Services, DTOs, validation, business rules, JWT/password logic   |
| **DAL** | `StoresPOS.DAL` | Entities, DbContext, EF configurations, migrations, repositories |

### Dependency direction

```text
PL  -->  BLL  -->  DAL
```

- Controllers talk to **BLL interfaces only**.
- Controllers **never** use `DbContext` or repositories directly.
- PL may reference DAL only at the composition root (`Program.cs`) for DI/EF setup.
- DAL never references PL or BLL.

---

## 4. Solution Structure Explained

### 4.1 Presentation Layer (`StoresPOS.PL`)

```text
StoresPOS.PL/
├── Controllers/          # HTTP endpoints (Account, Home, future modules)
├── Views/                # Razor screens per module
│   ├── Account/          # Login, AccessDenied
│   ├── Dashboard/
│   ├── POS/
│   ├── Products/
│   ├── Categories/
│   ├── Inventory/
│   ├── Sales/
│   ├── Purchases/
│   ├── Returns/
│   ├── Customers/
│   ├── Suppliers/
│   ├── Shifts/
│   ├── CashOuts/
│   ├── Reports/
│   ├── Users/
│   ├── Settings/
│   ├── Receipts/
│   └── Shared/           # Layouts and shared partials
├── ViewModels/           # Page-bound models
├── Extensions/           # App wiring helpers (JWT auth setup)
├── Filters/
├── Middlewares/
├── Resources/            # Localization resources (future)
└── wwwroot/
    ├── css/
    │   ├── theme.css     # Design tokens (colors, radius, shadows)
    │   ├── login.css
    │   └── site.css
    ├── js/
    ├── images/
    ├── fonts/
    └── lib/              # Local Bootstrap/jQuery (offline)
```

**What PL does:** show screens, accept input, call BLL, return results. No business rules here.

### 4.2 Business Logic Layer (`StoresPOS.BLL`)

```text
StoresPOS.BLL/
├── Interfaces/           # Service contracts (e.g. IAuthService)
├── Services/             # Business implementations
├── DTOs/                 # Transfer objects between PL and BLL
├── Models/               # Options/settings models (Jwt, AdminSeed)
├── Validators/           # Input/business validation
├── Mappings/             # Object mapping helpers
├── Exceptions/           # Domain/auth exceptions
├── Constants/            # Roles, auth constants
└── Extensions/           # BLL DI registration
```

**What BLL does today:** authentication, PBKDF2 hashing, JWT creation/validation, admin/role seeding rules.

**What BLL will do next:** products, purchases, shifts, POS sales, payments, inventory rules.

### 4.3 Data Access Layer (`StoresPOS.DAL`)

```text
StoresPOS.DAL/
├── Entities/             # Database models (User, Role, ...)
├── Context/              # ApplicationDbContext
├── Configurations/       # Fluent API table rules
├── Interfaces/           # Repository contracts (future)
├── Repositories/         # Persistence implementations (future)
├── Migrations/           # EF Core migrations
├── Seed/                 # Data seed helpers (optional)
└── Extensions/           # DAL DI + SQL Server registration
```

**What DAL does:** map entities to SQL Server, apply migrations, persist data. No UI and no business decisions.

---

## 5. Technology Stack

- **Framework:** ASP.NET Core MVC (.NET 8)
- **Architecture:** 3-Tier (PL, BLL, DAL)
- **Language:** C#
- **Database:** SQL Server / SQL Server Express (local)
- **ORM:** Entity Framework Core
- **Frontend:** Razor Views, HTML, CSS, JavaScript, local Bootstrap
- **Authentication:** Custom JWT (HttpOnly cookie for MVC)
- **Authorization:** Role-based (`Admin`, `Manager`, `Cashier`)
- **ASP.NET Core Identity:** Not used
- **Languages:** Arabic (RTL) and English (LTR)
- **Deployment:** Local / Offline

---

## 6. Main Feature Modules

### Product Management

- Nested categories
- Arabic/English names, barcode, unit
- Purchase / retail / wholesale prices
- Markup calculations
- Low-stock thresholds

### Purchases & Inventory

- Suppliers and purchase invoices
- Stock increases on purchase
- Inventory movement history
- Selling-price recalculation from markup

### POS & Sales

- Barcode scanning and product search
- Retail/wholesale mode
- Editable cart quantities
- Invoice-only price overrides (catalog prices stay unchanged)
- Discounts, hold/resume invoice
- Stock deduction on completed sale

### Payments

- Cash, Visa/Card, InstaPay, Vodafone Cash/Wallet
- Cash affects expected drawer cash
- Non-cash tracked separately

### Shift Management

- One globally open shift
- Multiple cashiers on the same shift
- Resume vs Close & Start New
- Optional cash reconciliation / Not Reconciled

### Cash Outs

- Amount + Reason only
- Linked to open shift and cashier
- Reduces expected drawer cash

### Additional Modules

- Customers, returns, dashboard, reports
- Users & roles, settings, backup/restore

---

## 7. User Roles

| Role        | Access                                                            |
| ----------- | ----------------------------------------------------------------- |
| **Admin**   | Full system: users, permissions, settings, reports                |
| **Manager** | Products, purchases, inventory, sales, returns, reports           |
| **Cashier** | POS, payments, shifts, cash-outs, allowed invoice price overrides |

---

## 8. Architecture Rules

1. Keep the three-tier structure.
2. Controllers never access `DbContext` directly.
3. Validate sensitive business rules server-side.
4. Use transactions for financial operations.
5. Use `decimal` for money.
6. No CDN/runtime internet dependency.
7. Do not add ASP.NET Core Identity.
8. Keep UI styling decisions separate from business rules.
9. Update `STATUS.md` after verified work.
10. Use `plan.md` as the roadmap source of truth.

---

## 9. UI / Design Direction

UI mockups define a clean retail style:

- Primary green: `#0F6751`
- Dark sidebar: `#1A1D26`
- Soft background: `#F5F7FA`
- Shared tokens live in `wwwroot/css/theme.css`

Goals:

- Minimal clicks
- Fast checkout
- Clear financial totals
- Arabic + English
- Responsive screens

---

## 10. Current Status Snapshot

Already in place:

- Solution + PL/BLL/DAL structure
- SQL Server connection + EF Core
- Custom JWT authentication and roles
- Login UI aligned with mockup design tokens

Next major phase:

- **Phase 5 — Inventory**

Track details in:

- `plan.md` → planned work
- `STATUS.md` → verified progress

---

## 11. Development Workflow

Before implementing a feature:

1. Read `plan.md`.
2. Read `STATUS.md`.
3. Follow 3-tier rules.
4. Implement only the requested scope.
5. Build and run relevant tests.
6. Update `STATUS.md`.
7. Update `plan.md` if roadmap/decisions change.

**Important:** Build a real maintainable POS system — not a static prototype. Prioritize simplicity, correctness, offline operation, and reliable sales/inventory calculations.
