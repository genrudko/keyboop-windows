using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Keyboop.Core;

namespace Keyboop.Windows;

/// <summary>
/// Deliberately limited keyboard integration test. Disabled until the user opts in.
/// Only our own process and notepad.exe are monitored; other applications are ignored.
/// This is NOT a secure-input detector or a production-ready automatic layout switcher.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private readonly HookProc _keyCallback;
    private readonly HookProc _mouseCallback;
    private readonly DispatcherTimer _delay;
    private IntPtr _keyHook;
    private IntPtr _mouseHook;
    private IntPtr _window;
    private bool _allowed;
    private bool _swallowedSpace;
    private readonly StringBuilder _word = new(128);
    private string _pendingWord = "";
    private IntPtr _pendingWindow;
    private DateTime _pendingSince;

    public event Action<string>? StatusChanged;
    private const int WH_KEYBOARD_LL=13, WH_MOUSE_LL=14;
    private const int WM_KEYDOWN=0x100, WM_SYSKEYDOWN=0x104, WM_KEYUP=0x101, WM_SYSKEYUP=0x105;
    private const int WM_LBUTTONDOWN=0x201, WM_RBUTTONDOWN=0x204, WM_MBUTTONDOWN=0x207,
        WM_MOUSEWHEEL=0x20A, WM_XBUTTONDOWN=0x20B, WM_MOUSEHWHEEL=0x20E;
    private const uint VK_SPACE=0x20, VK_BACK=0x08, VK_CONTROL=0x11, VK_MENU=0x12,
        VK_SHIFT=0x10, VK_LWIN=0x5B, VK_RWIN=0x5C;
    private const uint LLKHF_INJECTED=0x10, KEYEVENTF_KEYUP=0x0002,
        KEYEVENTF_UNICODE=0x0004, INPUT_KEYBOARD=1;

    public KeyboardHook()
    {
        _keyCallback=OnKeyboard;
        _mouseCallback=OnMouse;
        _delay=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(35) };
        _delay.Tick+=OnDelayedReplacement;
    }

    public void Start()
    {
        if(_keyHook!=IntPtr.Zero)return;
        if(Marshal.SizeOf<Input>()!=40)
            throw new InvalidOperationException("Unexpected Win64 INPUT ABI size: "+Marshal.SizeOf<Input>());
        _keyHook=SetWindowsHookEx(WH_KEYBOARD_LL,_keyCallback,GetModuleHandle(null),0);
        if(_keyHook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Keyboard hook failed");
        _mouseHook=SetWindowsHookEx(WH_MOUSE_LL,_mouseCallback,GetModuleHandle(null),0);
        if(_mouseHook==IntPtr.Zero)
        {
            var error=Marshal.GetLastWin32Error();
            Dispose();
            throw new Win32Exception(error,"Mouse hook failed");
        }
        Notify("Тестовый перехват включён — только это окно и Блокнот");
    }

    public void Dispose()
    {
        _delay.Stop();
        if(_keyHook!=IntPtr.Zero){UnhookWindowsHookEx(_keyHook);_keyHook=IntPtr.Zero;}
        if(_mouseHook!=IntPtr.Zero){UnhookWindowsHookEx(_mouseHook);_mouseHook=IntPtr.Zero;}
        Reset();
        _window=IntPtr.Zero;
        _allowed=false;
        GC.KeepAlive(_keyCallback);
        GC.KeepAlive(_mouseCallback);
    }

    private void Reset()
    {
        _word.Clear();
        _pendingWord="";
        _pendingWindow=IntPtr.Zero;
        _delay.Stop();
    }

    private void Notify(string value) => StatusChanged?.Invoke(value);

    private IntPtr OnMouse(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if(nCode>=0 && ((int)wParam is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN
            or WM_MOUSEWHEEL or WM_MOUSEHWHEEL or WM_XBUTTONDOWN))
        {
            // A click or scroll can reposition the caret without changing windows.
            Reset();
        }
        return CallNextHookEx(_mouseHook,nCode,wParam,lParam);
    }

    private IntPtr OnKeyboard(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if(nCode<0 || _keyHook==IntPtr.Zero)return Next(nCode,wParam,lParam);
        int msg=(int)wParam;
        if(msg!=WM_KEYDOWN && msg!=WM_SYSKEYDOWN && msg!=WM_KEYUP && msg!=WM_SYSKEYUP)
            return Next(nCode,wParam,lParam);
        var ev=Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        // Synthetic events must never recursively enter our typing buffer.
        if((ev.flags&LLKHF_INJECTED)!=0)return Next(nCode,wParam,lParam);

        if(ev.vkCode==VK_SPACE && (msg==WM_KEYUP||msg==WM_SYSKEYUP) && _swallowedSpace)
        {
            _swallowedSpace=false;
            return (IntPtr)1;
        }
        if(msg!=WM_KEYDOWN && msg!=WM_SYSKEYDOWN) return Next(nCode,wParam,lParam);

        var active=GetForegroundWindow();
        if(active!=_window)
        {
            Reset();
            _window=active;
            _allowed=IsTestWindow(active);
        }
        if(!_allowed)return Next(nCode,wParam,lParam);
        bool ctrl=Down(VK_CONTROL), alt=Down(VK_MENU);
        if(ev.vkCode==VK_SPACE && ctrl && alt && !Down(VK_LWIN) && !Down(VK_RWIN))
        {
            _swallowedSpace=true;
            _pendingWord=_word.ToString();
            _pendingWindow=active;
            _pendingSince=DateTime.UtcNow;
            _word.Clear();
            if(_pendingWord.Length>0) _delay.Start();
            return (IntPtr)1;
        }

        // Modifier key-down events do not clear the last word; we need it on the hotkey.
        if(ev.vkCode is VK_CONTROL or VK_MENU or VK_SHIFT or VK_LWIN or VK_RWIN)
            return Next(nCode,wParam,lParam);
        // Any other modifier chord invalidates the tracked text.
        if(ctrl||alt||Down(VK_LWIN)||Down(VK_RWIN))
        {
            Reset();
            return Next(nCode,wParam,lParam);
        }
        if(ev.vkCode==VK_BACK)
        {
            if(_word.Length>0)_word.Length--;
            return Next(nCode,wParam,lParam);
        }
        if(ev.vkCode is 0x25 or 0x26 or 0x27 or 0x28 or 0x24 or 0x23
            or 0x2E or 0x0D or 0x09 or 0x1B)
        {
            Reset();
            return Next(nCode,wParam,lParam);
        }
        var chars=GetChars(ev.vkCode,ev.scanCode,active);
        if(chars.Length==1 && (chars[0] is >= 'a' and <= 'z' or >= 'A' and <= 'Z'
                    or >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё'))
        {
            if(_word.Length<80)_word.Append(chars);
            else Reset();
        }
        else if(chars.Length>0)Reset();
        return Next(nCode,wParam,lParam);
    }

    private void OnDelayedReplacement(object? sender, EventArgs e)
    {
        if(_keyHook==IntPtr.Zero){Reset();return;}
        if((DateTime.UtcNow-_pendingSince)>TimeSpan.FromSeconds(2)){Reset();Notify("Отменено: хоткей удерживался слишком долго");return;}
        // Never type while Ctrl/Alt is still held: Backspace would become a shortcut.
        if(Down(VK_CONTROL)||Down(VK_MENU)||Down(VK_LWIN)||Down(VK_RWIN)
            ||Down(VK_SHIFT)||_swallowedSpace) return;
        _delay.Stop();
        var text=_pendingWord;
        var window=_pendingWindow;
        _pendingWord="";
        _pendingWindow=IntPtr.Zero;
        if(window==IntPtr.Zero || GetForegroundWindow()!=window || !IsTestWindow(window))
        {
            Notify("Отменено: окно ввода изменилось");
            return;
        }
        var converted=LayoutConverter.Convert(text);
        if(converted==text)return;
        try
        {
            int sent=Replace(text.Length,converted);
            Notify($"Исправлено: {text} → {converted} ({sent} событий)");
        }
        catch(Exception ex)
        {
            Notify("Ошибка SendInput: "+ex.Message);
        }
    }

    private IntPtr Next(int nCode,IntPtr wParam,IntPtr lParam)
        => CallNextHookEx(_keyHook,nCode,wParam,lParam);

    private static bool IsTestWindow(IntPtr hwnd)
    {
        if(hwnd==IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd,out uint id);
        if(id==0) return false;
        if(id==(uint)Environment.ProcessId)return true;
        try{return string.Equals(Process.GetProcessById((int)id).ProcessName,"notepad",
            StringComparison.OrdinalIgnoreCase);}
        catch{return false;}
    }

    private static bool Down(uint vk)=>(GetAsyncKeyState((int)vk)&0x8000)!=0;
    private static string GetChars(uint vk, uint scan, IntPtr active)
    {
        uint thread=GetWindowThreadProcessId(active,out _);
        ushort lang=(ushort)(GetKeyboardLayout(thread).ToInt64() & 0x03FF);
        bool shift=Down(VK_SHIFT), caps=(GetKeyState(0x14)&1)!=0;
        return PhysicalKeyMapper.TryMapLetter(vk,lang,shift,caps,out char ch)?ch.ToString():"";
    }
    private static int Replace(int deleteCount,string text)
    {
        var input=new List<Input>(deleteCount*2+text.Length*2);
        for(int i=0;i<deleteCount;i++)
        {
            input.Add(Key(VK_BACK,0));
            input.Add(Key(VK_BACK,KEYEVENTF_KEYUP));
        }
        foreach(char c in text)
        {
            input.Add(Unicode(c,0));
            input.Add(Unicode(c,KEYEVENTF_KEYUP));
        }
        if(input.Count==0)return 0;
        var sent=SendInput((uint)input.Count,input.ToArray(),Marshal.SizeOf<Input>());
        if(sent!=(uint)input.Count)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Only {sent}/{input.Count} keyboard events injected; inspect the text before retrying");
        return (int)sent;
    }
    private static Input Key(uint vk,uint flags)=>new()
        {type=INPUT_KEYBOARD,union=new(){ki=new(){wVk=(ushort)vk,dwFlags=flags}}};
    private static Input Unicode(char c,uint flags)=>new()
        {type=INPUT_KEYBOARD,union=new(){ki=new(){wScan=c,dwFlags=KEYEVENTF_UNICODE|flags}}};

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct { public uint vkCode,scanCode,flags,time;public UIntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput { public ushort wVk,wScan;public uint dwFlags,time;public UIntPtr dwExtraInfo; }
    // Win64 INPUT union is MOUSEINPUT-sized (32 bytes), not KEYBDINPUT-sized (24).
    [StructLayout(LayoutKind.Explicit, Size=32)]
    private struct InputUnion { [FieldOffset(0)] public KeyboardInput ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Input { public uint type;public InputUnion union; }

    [DllImport("user32.dll",SetLastError=true)]
    private static extern IntPtr SetWindowsHookEx(int idHook,HookProc lpfn,IntPtr hMod,uint threadId);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hhk,int nCode,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]private static extern short GetKeyState(int virtualKey);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr hWnd,out uint pid);
    [DllImport("user32.dll")]private static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll",SetLastError=true)]
    private static extern uint SendInput(uint cInputs,[In]Input[] pInputs,int cbSize);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? module);
}
