# تثبيت StoresPOS على جهاز المتجر (Windows)

> Phase 15. الإنتاج: Windows 10/11 أو Windows Server + SQL Server Express (أو أعلى).

## المتطلبات

1. Windows 64-bit
2. [.NET 8 Desktop Runtime / ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0) (Hosting Bundle لو هتشغّل كخدمة IIS؛ للتشغيل المباشر يكفي Runtime)
3. [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads) مع Mixed Mode أو Windows Auth
4. طابعة حرارية 80 مم (اختياري لكن موصى به) — راجع [`printing.md`](printing.md)

## 1) ثبّت SQL وأنشئ قاعدة البيانات

```powershell
# بعد تثبيت SQL Express، من مجلد المشروع:
dotnet tool restore
dotnet ef database update -p src/StoresPOS.DAL -s src/StoresPOS.PL
```

أو انسخ نسخة احتياطية `.bak` جاهزة واستعدها من **الإعدادات → قاعدة البيانات والنسخ**.

## 2) انشر التطبيق

```powershell
dotnet publish src/StoresPOS.PL -c Release -o C:\StoresPOS\App
```

عدّل `C:\StoresPOS\App\appsettings.json`:

- `ConnectionStrings:DefaultConnection`
- `Jwt:SecretKey` (سلسلة طويلة عشوائية)
- `License:IssuerSecret` (نفس السر اللي بنوقّع بيه ملفات الترخيص)
- `AdminSeed` لأول تشغيل فقط (احذف كلمة السر بعد أول دخول)

أنشئ مجلد النسخ الاحتياطي وامنحه صلاحية الكتابة لـ **SQL Server service account** (غالبًا `NT SERVICE\MSSQL$SQLEXPRESS`):

```text
C:\StoresPOS\Backups
```

## 3) شغّل التطبيق

```powershell
cd C:\StoresPOS\App
dotnet StoresPOS.PL.dll --urls http://127.0.0.1:5188
```

أو انشر على **IIS** — الخطوات التفصيلية في [`deployment.md`](deployment.md) (App Pool = No Managed Code، Physical path = `C:\StoresPOS\App`).

افتح http://127.0.0.1:5188 (أو بورت الـ binding) وادخل بـ `admin` / كلمة السر من `AdminSeed` (أو اللي غيّرتها).

## 4) إعدادات أول يوم

1. **إعادة تسجيل الدخول** بعد كل ترقية عشان JWT ياخد الصلاحيات الجديدة.
2. **الإعدادات:** ملف المتجر · الضريبة · المخزن البيّاع · الخزنة الافتراضية · مسار النسخ.
3. **الترخيص:** انسخ Device ID → ابعتّه لنا → الصق التوقيع في تاب الترخيص.
4. **طرق الدفع / الخزائن / المخازن** من الشاشات الخاصة بيهم.
5. اختصار الكاشير بـ Chrome kiosk — [`printing.md`](printing.md).

## 5) النسخ الاحتياطي

- يدوي: الإعدادات → نسخ احتياطي الآن
- تلقائي: عند إغلاق الوردية + مرة يوميًا في الساعة المضبوطة
- الاستعادة تستبدل قاعدة البيانات بالكامل — أوقف المستخدمين أولًا ثم أعد تشغيل التطبيق

## ترقية إصدار

1. خذ نسخة احتياطية
2. أوقف الخدمة
3. انشر فوق `C:\StoresPOS\App` (احتفظ بـ `App_Data` و `wwwroot/uploads`)
4. `dotnet ef database update` لو في migrations جديدة
5. شغّل وسجّل دخول من جديد
