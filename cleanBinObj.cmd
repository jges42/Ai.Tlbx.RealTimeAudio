@echo off
echo #################################################
echo # Projektordner sauber machen
echo #################################################
echo -
taskkill /IM MSBuild.exe /F 
taskkill /IM adb.exe /F
taskkill /IM node.exe /F
taskkill /IM VBCSCompiler.exe /F 
RD /S /Q TestResults 
del /S /F *.userprefs 
del /S /F *.user 
del /S /F *.bak 

:: Durchlaufen aller Ordner und Unterordner und Löschen von "bin" und "obj", wenn sie existieren
FOR /R %%X IN (bin,obj) DO (
    IF EXIST "%%X" (
        echo Loesche "%%X"
        RD /S /Q "%%X"
    )
)

:: DotNet Wiederherstellung
dotnet restore
dotnet restore

echo.
echo Script erfolgreich ausgefuehrt.