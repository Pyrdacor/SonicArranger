$ErrorActionPreference = 'Stop';

if ($isWindows) {
  Write-Host Copy nuget packages
  mkdir "nuget"
  xcopy /Y /I "SonicArranger\bin\Release\netstandard2.0\*.nupkg" "nuget\*"
}