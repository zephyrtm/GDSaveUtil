@echo off
setlocal

REM === Config ===
set SRC=main.cs
set OUT=GDSaveUtil.exe
set ICO=icon.ico
set JPG=icon.jpg

REM === Locate csc.exe ===
set CSCPATH=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSCPATH%" (
    set CSCPATH=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
)

if not exist "%CSCPATH%" (
    echo [!] Could not find csc.exe automatically.
    echo     Please edit this script and set CSCPATH manually.
    pause
    exit /b 1
)

REM === Decide on icon argument ===
set ICONARG=
if exist "%ICO%" (
    echo [+] Found %ICO%, using it as the icon.
    set ICONARG=/win32icon:%ICO%
) else (
    if exist "%JPG%" (
        echo [!] Found %JPG%, but .jpg cannot be used directly.
        echo     Please convert it to .ico first.
    ) else (
        echo [!] No icon.ico or icon.jpg found. Proceeding without custom icon...
    )
)

REM === Compile ===
echo [+] Compiling %SRC% to %OUT% ...
"%CSCPATH%" /target:exe /out:%OUT% %ICONARG% %SRC%

if errorlevel 1 (
    echo [!] Compilation failed.
    pause
    exit /b 1
)

echo [+] Build complete: %OUT%
pause