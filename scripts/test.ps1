$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$exe=Join-Path $root 'artifacts\DaktiloClient.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Run build.ps1 first'}
foreach($test in @('--self-test','--usb-self-test','--ui-self-test','--power-self-test')){
 $log=Join-Path $root ('artifacts\'+$test.TrimStart('-')+'.log')
 $process=Start-Process -FilePath $exe -ArgumentList @($test,$log) -WindowStyle Hidden -PassThru
 if(!$process.WaitForExit(60000)){$process.Kill();throw "$test timed out"}
 if($process.ExitCode -ne 0){if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log};throw "$test failed"}
 Write-Output "PASS $test"
}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if($version.ProductName -ne 'DaktiloClient' -or $version.FileVersion -ne '1.2.0.0'){throw 'Client product metadata mismatch'}
$assembly=[Reflection.Assembly]::LoadFile($exe)
foreach($name in @('RemoteDaktilo.RemoteSurface','RemoteDaktilo.UsbDashboard','RemoteDaktilo.UsbWindow','MrDaktilo.Files.FileWindow')){if($assembly.GetType($name)){throw "Private receiver UI type leaked: $name"}}
if($assembly.GetType('RemoteDaktilo.UsbBridge').GetMethod('Attach')){throw 'Receiver attachment API leaked'}
$logo=$assembly.GetManifestResourceStream('Daktilo.Brand.Logo')
$sha=[Security.Cryptography.SHA256]::Create()
try{if([BitConverter]::ToString($sha.ComputeHash($logo)).Replace('-','') -ne '050F6696DD4E50A46CED0D954164CF29ED09F316EBA25953EC8FB24F0669119A'){throw 'Original logo changed'}}finally{$logo.Dispose();$sha.Dispose()}
Write-Output 'PASS Client-only source build, product metadata and unchanged original logo'


