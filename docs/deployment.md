# نشر StoresPOS (offline)

## شكل التثبيت المقترح على جهاز المتجر

```text
C:\StoresPOS\
  App\                 # dotnet publish output
    App_Data\          # store-settings.json · license.json (يتكوّن تلقائيًا)
    wwwroot\uploads\   # شعار A4
  Backups\             # .bak — صلاحية SQL Server service
```

## تشغيل على IIS (موصى به للمتجر)

### مرة واحدة على الجهاز

1. ثبّت **IIS** (Features → Internet Information Services → ASP.NET / Application Development).
2. ثبّت [.NET 8 ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) ثم أعد تشغيل الجهاز أو نفّذ:
   ```powershell
   net stop was /y
   net start w3svc
   ```
3. ثبّت SQL Server Express وأنشئ قاعدة `StoresPOS` (أو اترك التطبيق يعمل migrations عند أول تشغيل لو مفعّل؛ الأفضل: `dotnet ef database update` من جهاز فيه SDK).

### Publish من جهاز التطوير

```powershell
# من جذر المشروع
dotnet publish src/StoresPOS.PL -c Release -o C:\StoresPOS\App
```

انسخ المجلد `C:\StoresPOS\App` للجهاز الهدف لو النشر من جهاز آخر.

عدّل `C:\StoresPOS\App\appsettings.json` على الجهاز الهدف:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=StoresPOS;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

(لو SQL باسم مختلف غيّر `Server=` — مثال: `Server=.;` أو `Server=localhost\\SQLEXPRESS`)

أنشئ:

```text
C:\StoresPOS\Backups
```

وأعطِ صلاحية **Modify** لـ:
- `IIS AppPool\StoresPOS` (مجلد التطبيق + `App_Data` + `wwwroot\uploads`)
- حساب خدمة SQL (غالبًا `NT SERVICE\MSSQL$SQLEXPRESS`) على `C:\StoresPOS\Backups`

### إنشاء الموقع في IIS Manager

1. **Application Pools** → Add → Name: `StoresPOS` · .NET CLR version: **No Managed Code** · Pipeline: Integrated
2. **Sites** → Add Website:
   - Site name: `StoresPOS`
   - Application pool: `StoresPOS`
   - Physical path: `C:\StoresPOS\App`
   - Binding: `http` · Port `5188` (أو `80`) · Host name فاضي للوكال
3. تأكد إن الموقع Started، وافتح: `http://localhost:5188`

لو ظهر خطأ 500.19 / 500.30: غالبًا Hosting Bundle مش متثبت أو الـ App Pool مش **No Managed Code**.

### صلاحيات مجلد التطبيق

```powershell
icacls "C:\StoresPOS\App" /grant "IIS AppPool\StoresPOS:(OI)(CI)M" /T
icacls "C:\StoresPOS\Backups" /grant "NT SERVICE\MSSQL`$SQLEXPRESS:(OI)(CI)M" /T
```

أنشئ يدويًا لو مش موجودين: `C:\StoresPOS\App\App_Data` و `C:\StoresPOS\App\wwwroot\uploads`.

### قاعدة البيانات أول مرة

من جهاز فيه .NET SDK ومشروع الكود (أو بعد ما تنسخ المشروع):

```powershell
dotnet ef database update -p src/StoresPOS.DAL -s src/StoresPOS.PL
```

أو استعد `.bak` من شاشة الإعدادات بعد أول دخول Admin.

---

## تشغيل كخدمة Windows بدون IIS (مثال nssm)

```powershell
nssm install StoresPOS "C:\Program Files\dotnet\dotnet.exe" "C:\StoresPOS\App\StoresPOS.PL.dll --urls http://127.0.0.1:5188"
nssm set StoresPOS AppDirectory C:\StoresPOS\App
nssm start StoresPOS
```

الكاشير يفتح Chrome على نفس العنوان (يفضّل `--kiosk-printing`).

## إصدار ملف ترخيص (نحن)

من جهاز التطوير، بعد ما المتجر يبعت Device ID من الإعدادات:

```powershell
# مثال: استخدم نفس IssuerSecret الموجود في appsettings على جهاز المتجر
dotnet run --project tools/LicenseIssuer -- `
  --device <DEVICE_ID> `
  --name "Al Noor Market" `
  --secret "CHANGE_ME_LICENSE_HMAC_SECRET_32CHARS_MIN!!"
```

أو بدون الأداة: التوقيع = HMAC-SHA256 بالـ UTF-8 للسلسلة:

```text
{DEVICE_ID_UPPER}|{LicensedTo}|{yyyy-MM-dd|never}
```

ثم النتيجة Hex uppercase.

## checklist قبل التسليم

- [ ] SQL Express شغّال والنسخ على `C:\StoresPOS\Backups` ينجح
- [ ] Admin دخل وغيّر كلمة السر
- [ ] الإعدادات: اسم المتجر · ضريبة · مخزن بيّاع · خزنة · طباعة
- [ ] الترخيص مفعّل
- [ ] بيع تجريبي + إيصال + إغلاق وردية + نسخ تلقائي
- [ ] اختصار الكاشير على سطح المكتب
