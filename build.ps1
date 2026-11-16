param([Parameter(Mandatory=$true)][string]$SdkDll)
$ErrorActionPreference='Stop'
if([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT){throw 'Build on Windows x64 with .NET SDK 10.'}
$tool=(Get-Command dotnet -ErrorAction Stop).Source
$SdkDll=(Resolve-Path -LiteralPath $SdkDll).Path
if([Reflection.AssemblyName]::GetAssemblyName($SdkDll).Version.ToString() -ne '2.1.0.0'){throw 'Use HIDMaestro.Core.dll 2.1.0.'}
$stage=Join-Path $PSScriptRoot '.build\src';$output=Join-Path $PSScriptRoot '.build\app'
New-Item -ItemType Directory -Path $stage,$output -Force|Out-Null
foreach($n in @('Program.cs','BridgeWindow.cs','EntryPoint.cs','KeyboardMap.cs','MouseState.cs','Profiles.cs','Bridge.csproj','app.manifest')){Copy-Item -LiteralPath (Join-Path ($PSScriptRoot+'\src') $n) -Destination $stage -Force}
Copy-Item -LiteralPath $SdkDll -Destination (Join-Path $stage 'HIDMaestro.Core.dll') -Force
& $tool build (Join-Path $stage 'Bridge.csproj') -c Release -o $output '-p:DebugType=None' '-p:DebugSymbols=false' '-p:ContinuousIntegrationBuild=true' ('-p:PathMap='+$stage+'=/src')
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
& $tool (Join-Path $output 'Aion2RemotePlay.dll') --self-test
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $output 'selftest.txt');throw 'Self-test failed.'}
Get-Content -LiteralPath (Join-Path $output 'selftest.txt')
Write-Output ('Ready: '+(Join-Path $output 'Aion2RemotePlay.exe'))
