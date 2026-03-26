$dir = $PSScriptRoot + "\"
$out = $dir + "\out"
Remove-Item -Force -Path ($out) -Recurse -ErrorAction SilentlyContinue

# KK -------------------------------------
Write-Output ("Creating KK release")

New-Item -ItemType Directory -Force -Path ($dir + "\release") | Out-Null
New-Item -ItemType Directory -Force -Path ($out + "\BepInEx\plugins") | Out-Null

Copy-Item -Path ($dir + "\bin\KK\*") -Destination ($out + "\BepInEx\plugins") -ErrorAction Stop -Force | Out-Null
# Copy-Item copies empty directories and I don't see any way to tell it to only copy files

$ver = Get-Date -Format "yyyy-MM-dd"
Write-Output ("Version " + $ver)
Compress-Archive -Path ($out + "\*") -Force -CompressionLevel "Optimal" -DestinationPath ($dir +"\release\Koik_Plugins_" + $ver + ".zip")

Remove-Item -Force -Path ($out) -Recurse -ErrorAction SilentlyContinue


# KKS ------------------------------------
Write-Output ("Creating KKS release")

New-Item -ItemType Directory -Force -Path ($out + "\BepInEx\plugins") | Out-Null

Copy-Item -Path ($dir + "\bin\KKS\*") -Destination ($out + "\BepInEx\plugins") -ErrorAction Stop -Force | Out-Null
# Copy-Item copies empty directories and I don't see any way to tell it to only copy files

$ver = Get-Date -Format "yyyy-MM-dd"
Write-Output ("Version " + $ver)
Compress-Archive -Path ($out + "\*") -Force -CompressionLevel "Optimal" -DestinationPath ($dir +"\release\KoikSunshine_Plugins_" + $ver + ".zip")

Remove-Item -Force -Path ($out) -Recurse -ErrorAction SilentlyContinue