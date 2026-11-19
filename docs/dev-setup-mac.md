# تشغيل StoresPOS على Mac (للتطوير فقط)

الإنتاج على Windows + SQL Server (`Server=.` في `appsettings.json`). على Mac مفيش SQL Server ولا Windows auth، فبنشغّل SQL في Docker ونحط connection string خاص بالجهاز في **user-secrets** (برّه الريبو، مش بيتعمله commit).

## المتطلبات

- .NET 8 SDK (`brew install --cask dotnet-sdk@8`)
- Docker عبر Colima (`brew install colima docker` ثم `colima start`)

## 1) SQL في Docker

صورة SQL Server 2022 الرسمية x86 فقط وتحتاج ≥ 2 GB رام؛ على Apple Silicon مع Colima الافتراضي (2 GB، بدون Rosetta) بنستخدم **Azure SQL Edge** (نفس محرك SQL Server، ARM أصلي):

```bash
docker run -d --name storespos-sql --restart unless-stopped \
  -e ACCEPT_EULA=1 -e "MSSQL_SA_PASSWORD=<كلمة-سر-قوية>" \
  -p 127.0.0.1:1433:1433 -v storespos-sql-data:/var/opt/mssql \
  mcr.microsoft.com/azure-sql-edge:latest
```

> Azure SQL Edge متوقّف دعمه من Microsoft — مناسب للتطوير المحلي فقط. البديل: `colima start --vm-type vz --vz-rosetta --memory 4` ثم صورة `mcr.microsoft.com/mssql/server:2022-latest` بـ `--platform linux/amd64`.

## 2) connection string (user-secrets)

```bash
cd src/StoresPOS.PL
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=127.0.0.1,1433;Database=StoresPOS;User Id=sa;Password=<نفس-كلمة-السر>;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

(محتاج `<UserSecretsId>` في `StoresPOS.PL.csproj` — بيتضاف بـ `dotnet user-secrets init` مرة واحدة.)

## 3) الداتابيز والتشغيل

```bash
dotnet tool restore
dotnet ef database update -p src/StoresPOS.DAL -s src/StoresPOS.PL
dotnet run --project src/StoresPOS.PL
```

افتح http://127.0.0.1:5188 — دخول التطوير من `appsettings.Development.json` (`AdminSeed`).

## مشاكل معروفة

- **Colima وقف أثناء سحب الصورة:** غالبًا الرام. `colima start` تاني، وكرّر `docker pull`.
- **`Could not open a connection to SQL Server`:** الكونتينر واقف (`docker start storespos-sql`) أو user-secrets مش متظبط.
