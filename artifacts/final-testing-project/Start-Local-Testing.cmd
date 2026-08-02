@echo off
setlocal
set "ROOT=%~dp0"

for %%P in (5000 5001) do (
  netstat -ano | findstr /R /C:":%%P .*LISTENING" >nul
  if not errorlevel 1 (
    echo.
    echo Cannot start local testing because port %%P is already in use.
    echo Close the application using that port, then run this file again.
    echo The Certificate Engine must use http://localhost:5000 and the Verification Website must use http://localhost:5001.
    echo.
    pause
    exit /b 1
  )
)

start "Certificate Engine - Management" /D "%ROOT%engine" "%ROOT%engine\CertificateEngine.exe" --urls http://localhost:5000
start "Certificate Verification Website" /D "%ROOT%verification" cmd /c "set Verification__DataDirectory=%ROOT%engine\data&& CertificateVerification.Web.exe --urls http://localhost:5001"

timeout /t 3 /nobreak >nul
curl --fail --silent http://localhost:5000/ >nul
if errorlevel 1 goto :startupFailed
curl --fail --silent http://localhost:5001/ >nul
if errorlevel 1 goto :startupFailed
start "" http://localhost:5001

echo Certificate Engine:       http://localhost:5000
echo Verification Website:     http://localhost:5001
echo.
echo Keep the two opened windows running while testing.
endlocal
exit /b 0

:startupFailed
echo.
echo One or both local services did not start. Check the two opened service windows for details.
pause
endlocal
exit /b 1
