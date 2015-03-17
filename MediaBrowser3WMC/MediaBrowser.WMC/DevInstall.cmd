@ECHO OFF
ECHO.
ECHO Usage: DevInstall.cmd [/u][/debug]
ECHO.
ECHO This script requires Administrative privileges to run properly.
ECHO Start > All Programs > Accessories> Right-Click Command Prompt > Select 'Run As Administrator'
ECHO.
 
set CompanyName=MediaBrowser
set AssemblyName=MediaBrowser
set RegistrationName=Registration
set ProgramImage=Application.png
set ProgramInActiveImage=ApplicationInactive.png

ver | find "6.1." > nul
	if %ERRORLEVEL% == 0 set RegistrationName=Registration7
 
ECHO.Determine whether we are on an 32 or 64 bit machine
if "%PROCESSOR_ARCHITECTURE%"=="x86" if "%PROCESSOR_ARCHITEW6432%"=="" goto x86
set ProgramFilesPath=c:\Program Files
ECHO.
 
goto unregister
 
:x86

    ECHO.On an x86 machine
    set ProgramFilesPath=%ProgramFiles%
    ECHO.

:unregister

    ECHO.*** Unregistering and deleting assemblies ***
    ECHO.

    ECHO.Unregister and delete previously installed files (which may fail if nothing is registered)
    ECHO.

    ECHO.Unregister the application entry points
    %windir%\ehome\RegisterMCEApp.exe /allusers "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\%RegistrationName%.xml" /u
    ECHO.

    ECHO.Remove the DLL from the Global Assembly cache
    "%ProgramFilesPath%\Microsoft SDKs\Windows\v7.1\bin\gacutil.exe" /u "%AssemblyName%"
    ECHO.

    ECHO.Delete the folder containing the DLLs and supporting files (silent if successful)
    rd /s /q "%ProgramFilesPath%\%CompanyName%\%AssemblyName%"
    rd /s /q "%ProgramFilesPath%\%CompanyName%
    ECHO.

    REM Exit out if the /u uninstall argument is provided, leaving no trace of program files.
    if "%1"=="/u" goto exit
                
:releasetype
 
    if "%1"=="/Debug" goto debug
    set ReleaseType=Release
    ECHO.
    goto checkbin
                
:debug
    set ReleaseType=Debug
    ECHO.
                
:checkbin
 
    if exist ".\bin\%ReleaseType%\%AssemblyName%.dll" goto register
    ECHO.Cannot find %ReleaseType% binaries.
    ECHO.Build solution as %ReleaseType% and run script again. 
    goto exit
                
:register

    ECHO.*** Copying and registering assemblies ***
    ECHO.

    ECHO.Create the path for the binaries and supporting files (silent if successful)
    md "%ProgramFilesPath%\%CompanyName%\%AssemblyName%"
    ECHO.
    
    ECHO.Copy the binaries to program files
    copy /y ".\bin\%ReleaseType%\%AssemblyName%.dll" "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\"
    ECHO.
    
    ECHO.Copy the registration XML to program files
    copy /y ".\%RegistrationName%.xml" "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\"
    ECHO.
    
    ECHO.Copy the program image to program files
    copy /y ".\Images\%ProgramImage%" "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\"
    ECHO.
    ECHO.Copy the program image to program files
    copy /y ".\Images\%ProgramInActiveImage%" "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\"
    ECHO.

    ECHO.Register the DLL with the global assembly cache - %AssemblyName%
    ECHO."%ProgramFilesPath%\Microsoft SDKs\Windows\v7.1\Bin\gacutil.exe" /if "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\%AssemblyName%.dll"
    "%ProgramFilesPath%\Microsoft SDKs\Windows\v7.1\Bin\gacutil.exe" /if "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\%AssemblyName%.dll"
    ECHO.

    ECHO.Register the application with Windows Media Center
    %windir%\ehome\RegisterMCEApp.exe /allusers "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\%RegistrationName%.xml"
    ECHO %windir%\ehome\RegisterMCEApp.exe /allusers "%ProgramFilesPath%\%CompanyName%\%AssemblyName%\%RegistrationName%.xml"
	
:exit
