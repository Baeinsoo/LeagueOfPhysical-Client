# 유니티 에디터의 "Scene(s) Have Been Modified" 창을 찾아 처리한다(run-editmode.sh가 부른다).
#   -Mode probe    : 창이 있으면 "modal" 출력
#   -Mode title    : 에디터 주 창 제목(어느 씬이 수정됐는지 가르는 데 쓴다)
#   -Mode dontsave : "Don't Save" 누름
#   -Mode cancel   : "Cancel" 누름(저장도 버림도 아니라 안전)
param([string]$Project = "LeagueOfPhysical-Client", [string]$Mode = "probe")

Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class LopModal {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, P p, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  public static string Text(IntPtr h) { var s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
  public static string Cls(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
}
"@

$editor = Get-Process Unity -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -like "$Project -*" } | Select-Object -First 1
if ($null -eq $editor) { exit 1 }
if ($Mode -eq "title") { Write-Output $editor.MainWindowTitle; exit 0 }

$script:dialog = [IntPtr]::Zero
[void][LopModal]::EnumWindows({ param($h, $l)
    $p = 0; [void][LopModal]::GetWindowThreadProcessId($h, [ref]$p)
    if ($p -eq $editor.Id -and [LopModal]::IsWindowVisible($h) -and [LopModal]::Cls($h) -eq "#32770" -and [LopModal]::Text($h) -eq "Scene(s) Have Been Modified") { $script:dialog = $h }
    $true }, [IntPtr]::Zero)
if ($script:dialog -eq [IntPtr]::Zero) { exit 0 }
if ($Mode -eq "probe") { Write-Output "modal"; exit 0 }

$want = if ($Mode -eq "dontsave") { "Don't Save" } else { "Cancel" }
$script:button = [IntPtr]::Zero
[void][LopModal]::EnumChildWindows($script:dialog, { param($h, $l)
    if ([LopModal]::Cls($h) -eq "Button" -and [LopModal]::Text($h) -eq $want) { $script:button = $h }
    $true }, [IntPtr]::Zero)
if ($script:button -eq [IntPtr]::Zero) { exit 2 }
[void][LopModal]::SendMessage($script:button, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)   # BM_CLICK
Write-Output "clicked $want"
