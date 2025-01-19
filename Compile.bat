dotnet.exe publish HPKISigner.csproj -c Release -r win-x64 --self-contained false -o "C:\VisualStudio\HPKISigner\publish\win-x64\SmallSize"

dotnet.exe publish HPKISigner.csproj -c Release -r win-x64 --self-contained -o "C:\VisualStudio\HPKISigner\publish\win-x64\LargeSize"

dotnet.exe publish HPKISigner.csproj -c Release -r linux-x64 --self-contained -o "C:\VisualStudio\HPKISigner\publish\linux-x64"

dotnet.exe publish HPKISigner.csproj -c Release -r osx-x64 --self-contained -o "C:\VisualStudio\HPKISigner\publish\osx-x64"

REM Other Runtime Identifiers (RIDs):
REM Windows: win-x86, win-arm, win-arm64
REM Linux: linux-arm, linux-arm64
REM macOS: osx-arm64

pause
