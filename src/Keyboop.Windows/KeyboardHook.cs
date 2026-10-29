using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Keyboop.Core;

namespace Keyboop.Windows;

/// <summary>Manual-only keyboard prototype. No disk storage, no clipboard access.
/// Must not be enabled for auto-conversion until secure-field + focus QA is done.</summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly HookProc _callback;
    private IntPtr _hook;
    private IntPtr _window;
    private readonly StringBuilder _word=new(128);
    private bool _swallowedHotkey;
    private const int WH_KEYBOARD_LL=13, WM_KEYDOWN=0x100, WM_SYSKEYDOWN=0x104,
        WM_KEYUP=0x101, WM_SYSKEYUP=0x105;
    private const uint LLKHF_INJECTED=0x10, VK_SPACE=0x20, VK_BACK=0x08,
        VK_CONTROL=0x11, VK_MENU=0x12, VK_SHIFT=0x10, VK_LWIN=0x5B, VK_RWIN=0x5C;
    private const uint KEYEVENTF_KEYUP=0x0002, KEYEVENTF_UNICODE=0x0004;
    private const uint INPUT_KEYBOARD=1;
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    public KeyboardHook(){_callback=Callback;}

    public void Start()
    {
        if(_hook!=IntPtr.Zero)return;
        // Low-level global hook receives events on the installing UI thread.
        _hook=SetWindowsHookEx(WH_KEYBOARD_LL,_callback,GetModuleHandle(null),0);
        if(_hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose()
    {
        var handle=_hook;_hook=IntPtr.Zero;
        if(handle!=IntPtr.Zero)UnhookWindowsHookEx(handle);
        _word.Clear();GC.KeepAlive(_callback);
    }
    private IntPtr Callback(int nCode,IntPtr wParam,IntPtr lParam)
    {
        if(nCode<0||_hook==IntPtr.Zero)return CallNextHookEx(_hook,nCode,wParam,lParam);
        var msg=(int)wParam;
        if(msg!=WM_KEYDOWN && msg!=WM_SYSKEYDOWN && msg!=WM_KEYUP && msg!=WM_SYSKEYUP)
            return CallNextHookEx(_hook,nCode,wParam,lParam);
        var ev=Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        if((ev.flags&LLKHF_INJECTED)!=0)return CallNextHookEx(_hook,nCode,wParam,lParam);
        if(ev.vkCode==VK_SPACE && (msg==WM_KEYUP||msg==WM_SYSKEYUP) && _swallowedHotkey)
        { _swallowedHotkey=false;return (IntPtr)1; }
        if(msg!=WM_KEYDOWN && msg!=WM_SYSKEYDOWN)return CallNextHookEx(_hook,nCode,wParam,lParam);

        var active=GetForegroundWindow();
        if(active!=_window){_window=active;_word.Clear();}
        if(active==IntPtr.Zero)return CallNextHookEx(_hook,nCode,wParam,lParam);

        var ctrl=Down(VK_CONTROL);var alt=Down(VK_MENU);
        if(ev.vkCode==VK_SPACE && ctrl && alt && !Down(VK_LWIN) && !Down(VK_RWIN))
        {
            _swallowedHotkey=true;
            var original=_word.ToString();
            _word.Clear();
            var corrected=LayoutConverter.Convert(original);
            if(original.Length>0 && original!=corrected && GetForegroundWindow()==active)
                Replace(original.Length,corrected);
            return (IntPtr)1;
        }
        // Modifier chords, navigation, delete, and editing commands invalidate buffer.
        if(ctrl||alt||Down(VK_LWIN)||Down(VK_RWIN)){_word.Clear();return CallNextHookEx(_hook,nCode,wParam,lParam);}
        if(ev.vkCode==VK_BACK){if(_word.Length>0)_word.Length--;return CallNextHookEx(_hook,nCode,wParam,lParam);}
        if(ev.vkCode==0x25||ev.vkCode==0x26||ev.vkCode==0x27||ev.vkCode==0x28||
           ev.vkCode==0x24||ev.vkCode==0x23||ev.vkCode==0x2E||ev.vkCode==0x0D||
           ev.vkCode==0x09||ev.vkCode==0x1B){_word.Clear();return CallNextHookEx(_hook,nCode,wParam,lParam);}

        var chars=GetChars(ev.vkCode,ev.scanCode,active);
        if(chars.Length==1 && (char.IsLetter(chars[0]) || chars[0]=='ё' || chars[0]=='Ё'))
        {
            if(_word.Length<100)_word.Append(chars);
            else _word.Clear();
        }
        else if(chars.Length>0)_word.Clear();
        return CallNextHookEx(_hook,nCode,wParam,lParam);
    }

    private static bool Down(uint vk)=>(GetAsyncKeyState((int)vk)&0x8000)!=0;
    private static string GetChars(uint vk,uint scan,IntPtr active)
    {
        var thread=GetWindowThreadProcessId(active,out _);
        var layout=GetKeyboardLayout(thread);
        var keys=new byte[256];if(!GetKeyboardState(keys))return "";
        var str=new StringBuilder(8);
        // Windows 10 1607+ bit 2: do not mutate dead key state.
        int n=ToUnicodeEx(vk,scan,keys,str,str.Capacity,4,layout);
        return n>0?str.ToString(0,Math.Min(n,str.Length)):"";
    }
    private static void Replace(int deleteCount,string text)
    {
        // One bounded SendInput batch: key-down/up backspaces then UTF-16 code units.
        var inputs=new List<Input>(deleteCount*2+text.Length*2);
        for(var i=0;i<deleteCount;i++){inputs.Add(Key(VK_BACK,0));inputs.Add(Key(VK_BACK,KEYEVENTF_KEYUP));}
        foreach(var ch in text){inputs.Add(Unicode(ch,0));inputs.Add(Unicode(ch,KEYEVENTF_KEYUP));}
        if(inputs.Count==0)return;
        _=SendInput((uint)inputs.Count,inputs.ToArray(),Marshal.SizeOf<Input>());
    }
    private static Input Key(uint vk,uint flags)=>new(){type=INPUT_KEYBOARD,union=new(){ki=new(){wVk=(ushort)vk,dwFlags=flags}}};
    private static Input Unicode(char c,uint flags)=>new(){type=INPUT_KEYBOARD,union=new(){ki=new(){wScan=c,dwFlags=KEYEVENTF_UNICODE|flags}}};

    [StructLayout(LayoutKind.Sequential)]private struct KbdLlHookStruct{public uint vkCode,scanCode,flags,time;public UIntPtr dwExtraInfo;}
    [StructLayout(LayoutKind.Sequential)]private struct KeyboardInput{public ushort wVk,wScan;public uint dwFlags,time;public UIntPtr dwExtraInfo;}
    [StructLayout(LayoutKind.Explicit)]private struct InputUnion{[FieldOffset(0)]public KeyboardInput ki;}
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint type;public InputUnion union;}

    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int idHook,HookProc lpfn,IntPtr hMod,uint threadId);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hhk,int nCode,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")]private static extern bool GetKeyboardState([Out]byte[] lpKeyState);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr hWnd,out uint pid);
    [DllImport("user32.dll")]private static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int ToUnicodeEx(uint wVirtKey,uint wScanCode,byte[] lpKeyState,StringBuilder pwszBuff,int cchBuff,uint wFlags,IntPtr dwhkl);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint cInputs,[In]Input[] pInputs,int cbSize);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? module);
}
