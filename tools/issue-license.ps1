param(
    [Parameter(Mandatory = $true)][string]$DeviceId,
    [Parameter(Mandatory = $true)][string]$LicensedTo,
    [Parameter(Mandatory = $true)][string]$Secret,
    [string]$Expires = "never",
    [string]$OutFile = ""
)

$payload = ("{0}|{1}|{2}" -f $DeviceId.Trim().ToUpperInvariant(), $LicensedTo.Trim(), $Expires)
$hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($Secret))
$sig = ([System.BitConverter]::ToString($hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($payload)))).Replace("-", "")

$expiresUtc = $null
if ($Expires -ne "never") {
    $expiresUtc = [DateTime]::ParseExact($Expires, "yyyy-MM-dd", [Globalization.CultureInfo]::InvariantCulture).ToString("o")
}

$obj = [ordered]@{
    DeviceId   = $DeviceId.Trim()
    LicensedTo = $LicensedTo.Trim()
    ExpiresUtc = $expiresUtc
    Signature  = $sig
}

$json = $obj | ConvertTo-Json
Write-Host $json
if ($OutFile) {
    Set-Content -Path $OutFile -Value $json -Encoding UTF8
    Write-Host "Wrote $OutFile"
}
