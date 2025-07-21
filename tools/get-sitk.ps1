# tools\get-sitk.ps1
param([string]$Tag = 'v2.5.0')

$zip = "$PSScriptRoot\sitk.zip"
gh release download $Tag `
    --repo SimpleITK/SimpleITK `
    --pattern 'SimpleITK-*CSharp-win64*.zip' `
    --output $zip

$tmp = "$PSScriptRoot\_tmp"
Expand-Archive $zip -DestinationPath $tmp -Force
$targetDlls = Get-ChildItem $tmp -Recurse -Include 'SimpleITKCSharpManaged.dll', 'SimpleITKCSharpNative.dll'

$feed = Join-Path (Split-Path $PSScriptRoot -Parent) "packages\sitk"
New-Item $feed -ItemType Directory -Force | Out-Null
Write-Host "Target directory for copy operation: $feed"

# Iterate through each found DLL and copy it
foreach ($dll in $targetDlls) {
    Write-Host "  Copying $($dll.Name)..."
    Copy-Item $dll.FullName $feed -Force
}

Write-Host "Cleaning up temporary files: '$zip' and '$tmp'..."
Remove-Item $zip, $tmp -Recurse -Force
Write-Host "Cleanup complete. Script finished."