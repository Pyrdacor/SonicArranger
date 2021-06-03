$ErrorActionPreference = 'Stop';

if ($isWindows) {
  Write-Host Publish Windows executables
  dotnet publish -c Release "./SonicConvert/SonicConvert.csproj" -p:PublishSingleFile=true -r win-x86 --no-restore --nologo --no-self-contained
  dotnet publish -c Release "./SonicConvert/SonicConvert.csproj" -p:PublishSingleFile=true -r win-x64 --no-restore --nologo --no-self-contained
  Write-Host Pack zips for Windows
  copy "SonicConvert\bin\Any CPU\Release\netcoreapp3.1\win-x64\publish\SonicConvert.exe" "SonicConvert\SonicConvert.exe"
  7z a SonicConvert-Windows.zip "SonicConvert\SonicConvert.exe"
  copy "SonicConvert\bin\Any CPU\Release\netcoreapp3.1\win-x86\publish\SonicConvert.exe" "SonicConvert\SonicConvert.exe"
  7z a SonicConvert-Windows32Bit.zip "SonicConvert\SonicConvert.exe"
  Write-Host Publish Windows standalone executables
  dotnet publish -c Release "./SonicConvert/SonicConvert.csproj" -p:PublishSingleFile=true -r win-x86 --no-restore --nologo --self-contained
  dotnet publish -c Release "./SonicConvert/SonicConvert.csproj" -p:PublishSingleFile=true -r win-x64 --no-restore --nologo --self-contained
  Write-Host Pack standalone zips for Windows
  7z a SonicConvert-Windows-Standalone.zip "SonicConvert\bin\Any CPU\Release\netcoreapp3.1\win-x64\publish\SonicConvert.exe"
  7z a SonicConvert-Windows32Bit-Standalone.zip "SonicConvert\bin\Any CPU\Release\netcoreapp3.1\win-x86\publish\SonicConvert.exe"
} else {
  Write-Host Publish Linux executable
  dotnet publish -c Release "./SonicConvert/SonicConvert.csproj" -p:PublishSingleFile=true -r linux-x64 --no-restore --no-self-contained
  Write-Host Pack tar for Linux
  7z a SonicConvert-Linux.tar "./SonicConvert/bin/Any CPU/Release/netcoreapp3.1/linux-x64/publish/SonicConvert"
  7z a SonicConvert-Linux.tar.gz SonicConvert-Linux.tar
  rm SonicConvert-Linux.tar
  Write-Host Publish Linux standalone executable
  dotnet publish -c Release "./SonicConvert/SonicConvert.csproj" -p:PublishSingleFile=true -r linux-x64 --no-restore --self-contained
  Write-Host Pack standalone tar for Linux
  7z a SonicConvert-Linux-Standalone.tar "./SonicConvert/bin/Any CPU/Release/netcoreapp3.1/linux-x64/publish/SonicConvert"
  7z a SonicConvert-Linux-Standalone.tar.gz SonicConvert-Linux-Standalone.tar
  rm SonicConvert-Linux-Standalone.tar
}