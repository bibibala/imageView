; ImageViewer Windows Installer
; Called by publish.sh with -D flags for each variable

; ========== Configurable variables (passed via -D by publish.sh) ==========
; The values below are fallback defaults only -- publish.sh overrides them from csproj.
!ifndef APP_NAME
  !define APP_NAME "ImageViewer"
!endif
!ifndef APP_TITLE
  !define APP_TITLE "Image"
!endif
!ifndef APP_VERSION
  !define APP_VERSION "1.1.0"
!endif
!ifndef APP_PUBLISHER
  !define APP_PUBLISHER "nightwish"
!endif
!ifndef OUTPUT_DIR
  !define OUTPUT_DIR "../publish/win-x64"
!endif
!ifndef ICON_PATH
  !define ICON_PATH "../src/Assets/icon.ico"
!endif
!ifndef LICENSE_PATH
  !define LICENSE_PATH "../LICENSE"
!endif

; ========== Base config ==========
Name "${APP_TITLE}"
OutFile "${OUTPUT_DIR}/../${APP_TITLE}-${APP_VERSION}-win-x64.exe"
InstallDir "$PROGRAMFILES64\${APP_TITLE}"
InstallDirRegKey HKLM "Software\${APP_PUBLISHER}\${APP_TITLE}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma

; ========== MUI2 ==========
!include "MUI2.nsh"

!define MUI_ABORTWARNING
!define MUI_ICON "${ICON_PATH}"
!define MUI_UNICON "${ICON_PATH}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LICENSE_PATH}"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ========== Sections ==========
Section "${APP_TITLE} (required)" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "${OUTPUT_DIR}\*.*"
  WriteUninstaller "$INSTDIR\uninstall.exe"

  WriteRegStr HKLM "Software\${APP_PUBLISHER}\${APP_TITLE}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\${APP_PUBLISHER}\${APP_TITLE}" "Version" "${APP_VERSION}"

  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}" "DisplayName" "${APP_TITLE}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}" "DisplayIcon" '"$INSTDIR\${APP_NAME}.exe"'
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}" "NoRepair" 1
SectionEnd

Section "Start Menu Shortcut" SecStartMenu
  CreateDirectory "$SMPROGRAMS\${APP_TITLE}"
  CreateShortcut "$SMPROGRAMS\${APP_TITLE}\${APP_TITLE}.lnk" "$INSTDIR\${APP_NAME}.exe"
  CreateShortcut "$SMPROGRAMS\${APP_TITLE}\Uninstall ${APP_TITLE}.lnk" "$INSTDIR\uninstall.exe"
SectionEnd

Section /o "Desktop Shortcut" SecDesktop
  CreateShortcut "$DESKTOP\${APP_TITLE}.lnk" "$INSTDIR\${APP_NAME}.exe"
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "Install ${APP_TITLE} application (required)."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} "Create a shortcut in the Start Menu."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Create a shortcut on the Desktop."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ========== Uninstall ==========
Section "Uninstall"
  Delete "$INSTDIR\uninstall.exe"
  RMDir /r "$INSTDIR"

  Delete "$SMPROGRAMS\${APP_TITLE}\*.*"
  RMDir "$SMPROGRAMS\${APP_TITLE}"
  Delete "$DESKTOP\${APP_TITLE}.lnk"

  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_TITLE}"
  DeleteRegKey HKLM "Software\${APP_PUBLISHER}\${APP_TITLE}"
SectionEnd
