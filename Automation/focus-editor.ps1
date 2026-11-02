# 유니티 에디터 창을 앞으로 가져온다 — 에디터가 뒤에 있거나 최소화돼 CLI 명령이 30초 타임아웃일 때 사람 대신 누른다.
# 윈도우는 뒤에 있는 프로그램이 다른 창을 앞으로 올리는 것을 막는다(SetForegroundWindow가 false). 키 입력이 막 들어온
# 프로세스에게는 허용하므로 Alt를 한 번 눌렀다 떼고 올린다(널리 쓰이는 우회).
#
# 사용: powershell -ExecutionPolicy Bypass -File Automation/focus-editor.ps1 [-Project LeagueOfPhysical-Client]
param([string]$Project = "LeagueOfPhysical-Client")

Add-Type @"
using System; using System.Runtime.InteropServices;
public static class LopFocus {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@

$editor = Get-Process Unity -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -like "$Project -*" } | Select-Object -First 1
if ($null -eq $editor) { Write-Output "없음: '$Project' 에디터 창을 못 찾았다"; exit 1 }

$h = $editor.MainWindowHandle
if ([LopFocus]::IsIconic($h)) { [void][LopFocus]::ShowWindow($h, 9) }   # 9 = SW_RESTORE

$VK_MENU = 0x12; $KEYUP = 0x2
[LopFocus]::keybd_event($VK_MENU, 0, 0, [UIntPtr]::Zero)
[LopFocus]::keybd_event($VK_MENU, 0, $KEYUP, [UIntPtr]::Zero)
$ok = [LopFocus]::SetForegroundWindow($h)

Start-Sleep -Milliseconds 200
$front = [LopFocus]::GetForegroundWindow() -eq $h
Write-Output "pid=$($editor.Id) set=$ok front=$front"
if (-not $front) { exit 2 }
