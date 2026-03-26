$dir = $PSScriptRoot + "\"
$out = $dir + "\out"
Remove-Item -Force -Path ($out) -Recurse -ErrorAction SilentlyContinue

# KK -------------------------------------
Write-Output ("Creating KK release")

New-Item -ItemType Directory -Force -Path ($dir + "\release") | Out-Null
New-Item -ItemType Directory -Force -Path ($out + "\BepInEx\plugins") | Out-Null

Copy-Item -Path ($dir + "\bin\*") -Destination ($out + "\BepInEx\plugins") -ErrorAction Stop -Force | Out-Null
# Copy-Item copies empty directories and I don't see any way to tell it to only copy files

$ver = "v" + (Get-ChildItem -Path ($dir + "\bin\KK_Blink.dll") -Force -ErrorAction Stop)[0].VersionInfo.FileVersion.ToString() -replace "([\d+\.]+?\d+)[\.0]*$", '${1}'
Write-Output ("Version " + $ver)
Compress-Archive -Path ($out + "\*") -Force -CompressionLevel "Optimal" -DestinationPath ($dir +"\release\KK_Plugins_" + $ver + ".zip")

Remove-Item -Force -Path ($out) -Recurse -ErrorAction SilentlyContinue


# KKS ------------------------------------
Write-Output ("Creating KKS release")

New-Item -ItemType Directory -Force -Path ($out + "\BepInEx\plugins") | Out-Null

Copy-Item -Path ($dir + "\bin\*") -Destination ($out + "\BepInEx\plugins") -ErrorAction Stop -Force | Out-Null
# Copy-Item copies empty directories and I don't see any way to tell it to only copy files

$ver = "v" + (Get-ChildItem -Path ($dir + "\bin\KKS_Blink.dll") -Force -ErrorAction Stop)[0].VersionInfo.FileVersion.ToString() -replace "([\d+\.]+?\d+)[\.0]*$", '${1}'
Write-Output ("Version " + $ver)
Compress-Archive -Path ($out + "\*") -Force -CompressionLevel "Optimal" -DestinationPath ($dir +"\release\KKS_Plugins_" + $ver + ".zip")

Remove-Item -Force -Path ($out) -Recurse -ErrorAction SilentlyContinue