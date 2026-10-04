; Per-user Holdhint installer. No administrator rights.
; Build: makensis -DARCH=x64 -DVER=1.2.0 -DSTAGE=... -DOUTDIR=... -DICON=... holdhint.nsi

Unicode True
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!ifndef ARCH
  !error "Pass -DARCH=x64 or -DARCH=arm64"
!endif
!ifndef VER
  !error "Pass -DVER=1.2.0"
!endif
!ifndef STAGE
  !error "Pass -DSTAGE=path to Holdhint.exe, holdhint.ico, and LICENSE.txt"
!endif
!ifndef OUTDIR
  !error "Pass -DOUTDIR=path for the setup program"
!endif
!ifndef ICON
  !error "Pass -DICON=path to holdhint.ico"
!endif

!define APP "Holdhint"
Name "${APP}"
OutFile "${OUTDIR}/Holdhint-${VER}-win-${ARCH}-setup.exe"
InstallDir "$LOCALAPPDATA\Holdhint"
InstallDirRegKey HKCU "Software\Holdhint" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
BrandingText "${APP} ${VER}"
ManifestSupportedOS all

!define MUI_ICON "${ICON}"
!define MUI_UNICON "${ICON}"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "${APP}"
!define MUI_WELCOMEPAGE_TEXT "Holdhint shows a panel of shortcuts while you hold Ctrl, Alt, Shift, or the Windows key.$\r$\n$\r$\nThis program is not signed. If Windows says it protected your PC, choose More info, then Run anyway.$\r$\n$\r$\nHoldhint installs for your account only. It does not need an administrator."
!define MUI_FINISHPAGE_RUN "$INSTDIR\Holdhint.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start Holdhint"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

VIProductVersion "${VER}.0"
VIAddVersionKey "ProductName" "Holdhint"
VIAddVersionKey "FileDescription" "Holdhint setup"
VIAddVersionKey "FileVersion" "${VER}.0"
VIAddVersionKey "ProductVersion" "${VER}"
VIAddVersionKey "CompanyName" "Joost Jacob"
VIAddVersionKey "LegalCopyright" "Copyright 2026 Joost Jacob"

!if "${ARCH}" == "arm64"
Function .onInit
  ${IfNot} ${IsNativeARM64}
    MessageBox MB_ICONSTOP|MB_OK "This installer is for a Windows PC with an ARM processor. On this PC, run the x64 installer instead."
    Abort
  ${EndIf}
FunctionEnd
!else
Function .onInit
  ; The x64 build is allowed on ARM, where Windows runs it under emulation.
  ${IfNot} ${IsNativeAMD64}
  ${AndIfNot} ${IsNativeARM64}
    MessageBox MB_ICONSTOP|MB_OK "Holdhint needs 64-bit Windows 10 or Windows 11."
    Abort
  ${EndIf}
FunctionEnd
!endif

Section "Holdhint" SecApp
  SectionIn RO
  ; A running copy locks the exe. 128 means it was not running.
  nsExec::Exec "taskkill /F /IM Holdhint.exe"
  Pop $0
  ; A previous install may have registered startup. The optional section writes it again.
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Holdhint"
  SetOutPath "$INSTDIR"
  File "${STAGE}/Holdhint.exe"
  File "${STAGE}/holdhint.ico"
  File "${STAGE}/LICENSE.txt"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS"
  CreateShortcut "$SMPROGRAMS\Holdhint.lnk" "$INSTDIR\Holdhint.exe" "" "$INSTDIR\holdhint.ico"
  WriteRegStr HKCU "Software\Holdhint" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint" "DisplayName" "Holdhint"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint" "DisplayVersion" "${VER}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint" "Publisher" "Joost Jacob"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint" "DisplayIcon" "$INSTDIR\holdhint.ico"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint" "NoRepair" 1
SectionEnd

Section "Start Holdhint when I sign in" SecStartup
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Holdhint" '"$INSTDIR\Holdhint.exe"'
SectionEnd

LangString DESC_App ${LANG_ENGLISH} "The Holdhint program, its icon, and the license."
LangString DESC_Startup ${LANG_ENGLISH} "Opens Holdhint when you sign in. You can change this later from the Holdhint menu."

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecApp} $(DESC_App)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartup} $(DESC_Startup)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  nsExec::Exec "taskkill /F /IM Holdhint.exe"
  Pop $0
  Delete "$SMPROGRAMS\Holdhint.lnk"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Holdhint"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Holdhint"
  DeleteRegKey HKCU "Software\Holdhint"
  ; shortcuts.json and settings.json live in %APPDATA%\Holdhint and are kept.
  Delete "$INSTDIR\Holdhint.exe"
  Delete "$INSTDIR\holdhint.ico"
  Delete "$INSTDIR\LICENSE.txt"
  Delete "$INSTDIR\holdhint.log"
  Delete "$INSTDIR\holdhint.log.old"
  Delete "$INSTDIR\self-test.txt"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
