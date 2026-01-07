# Generate strong-name key for MultiGpuHelper
# This uses the .NET Framework StrongNameKeyPair API

param(
    [string]$OutputFile = "MultiGpuHelper.snk"
)

try {
    Write-Host "Generating strong-name key: $OutputFile"

    # Create a temporary RSA key
    $tempPath = [System.IO.Path]::GetTempFileName()

    # Use a process to call sn.exe if available, otherwise use managed code
    $snPath = "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\sn.exe"

    if (Test-Path $snPath) {
        Write-Host "Using sn.exe from SDK"
        & "$snPath" -k $OutputFile
    } else {
        # Fallback: Create using .NET Reflection Emit with cryptography
        # Generate RSA key using managed API
        $cspParams = New-Object System.Security.Cryptography.CspParameters
        $cspParams.ProviderType = 1  # PROV_RSA_FULL
        $cspParams.KeyNumber = 0  # AT_SIGNATURE

        $rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider(2048, $cspParams)

        # Export key pair in PUBLICKEYBLOB format
        $publicKeyBlob = $rsa.ExportCspBlob($false)
        $privateKeyBlob = $rsa.ExportCspBlob($true)

        # Write SNK file with correct format
        # SNK structure: 0xFDF40000 (magic/version) + key data
        [byte[]]$snkHeader = @(0xFD, 0xF4, 0x00, 0x00)

        $snkBytes = $snkHeader + $privateKeyBlob
        [System.IO.File]::WriteAllBytes($OutputFile, $snkBytes)

        Write-Host "Generated SNK using managed RSA"
    }

    if (Test-Path $OutputFile) {
        $fileSize = (Get-Item $OutputFile).Length
        Write-Host "SUCCESS - Strong-name key generated: $OutputFile"
        Write-Host "File size: $fileSize bytes"
        exit 0
    } else {
        Write-Host "ERROR: SNK file was not created"
        exit 1
    }
}
catch {
    Write-Host "ERROR: $_"
    Write-Host $_.Exception.Message
    exit 1
}
