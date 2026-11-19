using Microsoft.Extensions.Options;
using StoresPOS.BLL.DTOs;
using StoresPOS.BLL.Exceptions;
using StoresPOS.BLL.Models;
using StoresPOS.BLL.Services;

namespace StoresPOS.BLL.Tests;

public class SettingsAndLicenseTests
{
    [Fact]
    public async Task StoreSettings_Save_RoundTripsThroughJsonFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "storespos-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, "{}");
            var monitor = TestFactories.StoreMonitor(new StoreSettings { NameEn = "Old", NameAr = "قديم" });
            var svc = new StoreSettingsService(
                monitor,
                Options.Create(new StoreSettingsPersistenceOptions { FilePath = path }));

            await svc.SaveAsync(new StoreSettingsFormDto
            {
                NameEn = "Al Noor",
                NameAr = "النور",
                ReceiptLanguage = "ar",
                ReceiptDigits = "western",
                DefaultReceiptFormat = "A4",
                PaperWidthMm = 80,
                VatPercent = 14m,
                InvoiceNumberPrefix = "INV",
                ReturnNumberPrefix = "RET",
                BackupFolder = @"C:\StoresPOS\Backups"
            });

            var json = await File.ReadAllTextAsync(path);
            Assert.Contains("Al Noor", json, StringComparison.Ordinal);
            Assert.Contains("\"DefaultReceiptFormat\": \"A4\"", json, StringComparison.Ordinal);
            Assert.Contains("\"InvoiceNumberPrefix\": \"INV-\"", json, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task License_Activate_AcceptsValidSignature_RejectsWrongDevice()
    {
        var licensePath = Path.Combine(Path.GetTempPath(), "storespos-license-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var secret = "TEST_LICENSE_SECRET_KEY_32_CHARS!!";
            var svc = new LicenseService(
                Options.Create(new StoreSettingsPersistenceOptions { LicenseFilePath = licensePath }),
                Options.Create(new LicenseOptions { IssuerSecret = secret }),
                TimeProvider.System);

            var deviceId = svc.GetDeviceId();
            var sig = svc.Sign(deviceId, "Test Store", null);
            var status = await svc.ActivateAsync(new LicenseActivateDto
            {
                DeviceId = deviceId,
                LicensedTo = "Test Store",
                Signature = sig
            });

            Assert.True(status.IsActivated);
            Assert.Equal("Test Store", status.LicensedTo);

            await Assert.ThrowsAsync<BusinessException>(() => svc.ActivateAsync(new LicenseActivateDto
            {
                DeviceId = "OTHERDEVICE1234",
                LicensedTo = "Test Store",
                Signature = sig
            }));
        }
        finally
        {
            if (File.Exists(licensePath))
            {
                File.Delete(licensePath);
            }
        }
    }
}
