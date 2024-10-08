@echo off
setlocal enabledelayedexpansion

:: 변수 설정
set "PROTOC=..\Tools\Protobuf\protoc-28.2-win64\bin\protoc.exe"
set "PROTO_PATH=..\Protos"
set "INCLUDE_PATH=..\Tools\Protobuf\protoc-28.2-win64\include"
set "OUT_PATH=..\Assets\Scripts\Generated\Protobuf"
set "file_count=0"

:: protoc 존재 확인
if not exist "%PROTOC%" (
    echo [ERROR] protoc compiler not found at %PROTOC%
    echo Please ensure the protoc compiler is installed correctly.
    goto :pause_exit
)

:: PROTO_PATH 존재 확인
if not exist "%PROTO_PATH%" (
    echo [ERROR] Proto file directory not found at %PROTO_PATH%
    echo Please ensure the directory exists and contains .proto files.
    goto :pause_exit
)

:: OUT_PATH 생성 (없는 경우)
if not exist "%OUT_PATH%" (
    mkdir "%OUT_PATH%" 2>nul
    if errorlevel 1 (
        echo [ERROR] Unable to create output directory %OUT_PATH%
        goto :pause_exit
    )
)

:: 컴파일 시작
echo Starting Proto compilation...
for %%F in ("%PROTO_PATH%\*.proto") do (
    if exist "%%F" (
        echo Compiling %%~nxF...
        "%PROTOC%" --proto_path="%PROTO_PATH%" --proto_path="%INCLUDE_PATH%" --csharp_out="%OUT_PATH%" "%%F"
        if errorlevel 1 (
            echo [ERROR] Failed to compile %%~nxF
            goto :pause_exit
        )
        set /a "file_count+=1"
    )
)

:: 결과 출력
if !file_count! equ 0 (
    echo [WARNING] No .proto files found in %PROTO_PATH%
) else (
    echo Successfully compiled !file_count! .proto file^(s^).
    echo Proto compilation complete.
    echo Generated files can be found in %OUT_PATH%
)

:pause_exit
echo:
echo Script execution complete.
echo Press any key to close this window...
pause >nul
endlocal