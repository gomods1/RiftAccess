# Keeps the game window in front for a while (a playtest would pause on focus loss). Stops by itself.
param([int]$Seconds = 120)
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class HF {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr p);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public static void Front(IntPtr h) {
    uint fg = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero), self = GetCurrentThreadId();
    keybd_event(0x12, 0, 0, UIntPtr.Zero); keybd_event(0x12, 0, 2, UIntPtr.Zero);
    AttachThreadInput(self, fg, true); ShowWindow(h, 9); BringWindowToTop(h); SetForegroundWindow(h); AttachThreadInput(self, fg, false);
  }
}
"@
$end = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $end) {
  $p = Get-Process RiftOfTheNecroDancer -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $p) { break }
  if ([HF]::GetForegroundWindow() -ne $p.MainWindowHandle) { [HF]::Front($p.MainWindowHandle) }
  Start-Sleep -Milliseconds 250
}
