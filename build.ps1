$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path -LiteralPath $csc)){throw '.NET Framework x64 compiler required'}
$out=Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $out | Out-Null
$files=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object {$_.FullName})
$args=@('/nologo','/target:winexe','/platform:x64','/optimize+','/main:RemoteDaktilo.Program','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.ServiceProcess.dll',"/out:$out\DaktiloClient.exe","/win32icon:$root\assets\client.ico","/resource:$root\assets\logo.png,Daktilo.Brand.Logo","/resource:$root\assets\client.ico,Daktilo.Brand.Icon","/resource:$root\third-party\usbipd.msi,Daktilo.UsbHost","/resource:$root\third-party\usbipd-source.tar.gz,Daktilo.Source.usbipd-source.tar.gz","/resource:$root\third-party\GPL-3.0.txt,Daktilo.License.GPL-3.0.txt","/resource:$root\third-party\COPYING.md,Daktilo.License.COPYING.md","/resource:$root\third-party\NOTICE.txt,Daktilo.License.NOTICE.txt")
& $csc @args @files
if($LASTEXITCODE -ne 0){throw 'Client source build failed'}
Write-Output "Built $out\DaktiloClient.exe (unsigned)"
