using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CheerCustom {
    public sealed class CodexPresentationSample {public string Stage,Time;public long Window;public uint Show,PlacementFlags;public bool Minimized,Maximized;public int Left,Top,Right,Bottom;}
    public sealed class CodexPresentationTrace {public string Started,Route,Error="",DirectFocusError="";public bool Success;public System.Collections.Generic.List<CodexPresentationSample> Samples=new System.Collections.Generic.List<CodexPresentationSample>();}
    [ComImport,Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface CodexActivationManager {
        [PreserveSig] int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string appId,[MarshalAs(UnmanagedType.LPWStr)]string arguments,uint options,out uint processId);
        [PreserveSig] int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)]string appId,IntPtr items,[MarshalAs(UnmanagedType.LPWStr)]string verb,out uint processId);
        [PreserveSig] int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)]string appId,IntPtr items,out uint processId);
    }
    sealed class CodexLaunchResult {
        public bool Visible,Foreground;
        public bool Raised;
        public long Window,ForegroundWindow;
        public uint ActivatedProcess;
        public string Description {get{return Foreground?"Codex窗口已显示并处于前台。":"Codex窗口已恢复，但前台切换尚未成功。";}}
    }
    static class CodexLauncher {
        [StructLayout(LayoutKind.Sequential)] struct FlashInfo {public uint Size;public IntPtr Window;public uint Flags,Count,Timeout;}
        [StructLayout(LayoutKind.Sequential)] struct WindowPoint {public int X,Y;}
        [StructLayout(LayoutKind.Sequential)] struct WindowRect {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)] struct Placement {public uint Length,Flags,Show;public WindowPoint Min,Max;public WindowRect Normal;}
        sealed class Presentation {public Placement Placement;public WindowRect Bounds;public bool WasMinimized;}
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FlashInfo info);
        static void StopFlash(IntPtr window){var info=new FlashInfo {Size=(uint)Marshal.SizeOf(typeof(FlashInfo)),Window=window,Flags=0,Count=0,Timeout=0};FlashWindowEx(ref info);}
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int command);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool GetWindowPlacement(IntPtr window,ref Placement placement);
        [DllImport("user32.dll")] static extern bool SetWindowPlacement(IntPtr window,ref Placement placement);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out WindowRect rect);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint processId);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process,ref uint length,StringBuilder value);
        [DllImport("ole32.dll")] static extern int CoCreateInstance(ref Guid clsid,IntPtr outer,uint context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out CodexActivationManager manager);
        [DllImport("ole32.dll")] static extern int CoAllowSetForegroundWindow([MarshalAs(UnmanagedType.IUnknown)]object manager,IntPtr reserved);
        static bool Allowed(string path){return CodexDiscovery.DesktopExecutable(path);}
        internal static string RunningApplicationId(IntPtr window){
            uint pid;GetWindowThreadProcessId(window,out pid);var process=OpenProcess(0x1000,false,pid);if(process==IntPtr.Zero)throw new InvalidOperationException("无法查询Codex应用身份。");
            try{uint length=0;int code=GetApplicationUserModelId(process,ref length,null);if(code==15703)return null;if(code!=122||length<2||length>1024)throw new InvalidOperationException("Codex应用身份查询失败："+code);var value=new StringBuilder((int)length);code=GetApplicationUserModelId(process,ref length,value);if(code!=0||!CodexDiscovery.PackageIdentity(value.ToString()))throw new InvalidOperationException("运行窗口的Codex应用身份不匹配。");return value.ToString();}finally{CloseHandle(process);}
        }
        static uint ActivateApplication(string appId){
            Guid clsid=new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"),iid=typeof(CodexActivationManager).GUID;CodexActivationManager manager;
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid,IntPtr.Zero,4,ref iid,out manager));
            try{CoAllowSetForegroundWindow(manager,IntPtr.Zero);uint pid;Marshal.ThrowExceptionForHR(manager.ActivateApplication(appId,"",0,out pid));return pid;}finally{Marshal.ReleaseComObject(manager);}
        }
        static bool FocusExistingWindow(IntPtr window,Presentation presentation){
            // Use only this exact HWND's WindowPattern. No desktop/control-tree search.
            var runtime=RuntimeEnvironment.GetRuntimeDirectory();var client=Assembly.LoadFrom(Path.Combine(runtime,"WPF","UIAutomationClient.dll"));var types=Assembly.LoadFrom(Path.Combine(runtime,"WPF","UIAutomationTypes.dll"));
            var elementType=client.GetType("System.Windows.Automation.AutomationElement",true);var element=elementType.GetMethod("FromHandle").Invoke(null,new object[]{window});var windowType=client.GetType("System.Windows.Automation.WindowPattern",true);
            var pattern=windowType.GetField("Pattern").GetValue(null);var provider=elementType.GetMethod("GetCurrentPattern").Invoke(element,new[]{pattern});var stateType=types.GetType("System.Windows.Automation.WindowVisualState",true);
            bool maximized=presentation!=null&&presentation.Placement.Show==3;windowType.GetMethod("SetWindowVisualState").Invoke(provider,new[]{Enum.Parse(stateType,maximized?"Maximized":"Normal")});
            for(int i=0;i<10&&!IsTargetForeground(window);i++)Thread.Sleep(25);return IsTargetForeground(window);
        }
        static CodexLaunchResult ExistingResult(IntPtr window){RequireVisible(IsWindowVisible(window),IsIconic(window));if(!IsTargetForeground(window))throw new InvalidOperationException("Codex窗口没有处于前台。");uint pid;GetWindowThreadProcessId(window,out pid);return new CodexLaunchResult {Visible=true,Foreground=true,Raised=true,Window=window.ToInt64(),ForegroundWindow=GetForegroundWindow().ToInt64(),ActivatedProcess=pid};}
        public static string Resolve(){return CodexDiscovery.Resolve().ToString();}
        static IntPtr FindWindow(){
            foreach(var p in Process.GetProcesses())using(p){try{if(p.MainWindowHandle!=IntPtr.Zero&&Allowed(p.MainModule.FileName))return p.MainWindowHandle;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}}
            return IntPtr.Zero;
        }
        public static string Minimize(){return MinimizeWindow(FindWindow());}
        internal static string MinimizeWindow(IntPtr window){
            if(window==IntPtr.Zero)return "Codex 未运行，无需收回。";
            if(IsIconic(window))return "Codex 已经最小化。";
            uint process;if(GetWindowThreadProcessId(window,out process)==GetCurrentThreadId())ShowWindow(window,6);else ShowWindowAsync(window,6);
            for(int i=0;i<30&&!IsIconic(window);i++)Thread.Sleep(25);
            if(!IsIconic(window))throw new InvalidOperationException("Codex 窗口未能最小化，请重试。");
            return "Codex 已收回到任务栏。";
        }
        internal static bool IsTargetForeground(IntPtr window){var front=GetForegroundWindow();return front==window||GetAncestor(front,3)==GetAncestor(window,3);}
        internal static void RequireVisible(bool visible,bool minimized){if(!visible||minimized)throw new InvalidOperationException("Codex窗口未能显示或恢复。");}
        internal static bool IsAbove(IntPtr window,IntPtr reference){if(window==reference)return true;for(var previous=GetWindow(reference,3);previous!=IntPtr.Zero;previous=GetWindow(previous,3))if(previous==window)return true;return false;}
        static Presentation RememberPresentation(IntPtr window){if(!IsWindowVisible(window)&&!IsIconic(window))return null;var placement=new Placement {Length=(uint)Marshal.SizeOf(typeof(Placement))};WindowRect bounds;if(!GetWindowPlacement(window,ref placement)||!GetWindowRect(window,out bounds))throw new InvalidOperationException("无法读取Codex原窗口状态。");bool minimized=IsIconic(window);if(minimized)placement.Show=(placement.Flags&2)!=0?3u:1u;return new Presentation {Placement=placement,Bounds=bounds,WasMinimized=minimized};}
        static void PreservePresentation(IntPtr window,Presentation prior){if(prior==null)return;var now=new Placement {Length=(uint)Marshal.SizeOf(typeof(Placement))};if(!GetWindowPlacement(window,ref now))throw new InvalidOperationException("无法核对Codex窗口状态。");if(now.Show!=prior.Placement.Show){var placement=prior.Placement;if(!SetWindowPlacement(window,ref placement))throw new InvalidOperationException("Codex原窗口状态恢复失败。");}
            // Maximization belongs to Windows. Do not resize a maximized window to
            // a saved rectangle: frame/DPI differences can turn it into a normal window.
            if(prior.Placement.Show==3||prior.WasMinimized)return;
            WindowRect bounds;if(!GetWindowRect(window,out bounds))throw new InvalidOperationException("无法核对Codex窗口尺寸。");var expected=prior.Bounds;if(bounds.Left!=expected.Left||bounds.Top!=expected.Top||bounds.Right!=expected.Right||bounds.Bottom!=expected.Bottom)if(!SetWindowPos(window,IntPtr.Zero,expected.Left,expected.Top,expected.Right-expected.Left,expected.Bottom-expected.Top,0x214))throw new InvalidOperationException("Codex原位置与尺寸恢复失败。");}
        static CodexPresentationSample SamplePresentation(string stage,IntPtr window){var placement=new Placement {Length=(uint)Marshal.SizeOf(typeof(Placement))};WindowRect bounds;if(!GetWindowPlacement(window,ref placement)||!GetWindowRect(window,out bounds))throw new InvalidOperationException("无法采集Codex窗口状态。");return new CodexPresentationSample {Stage=stage,Time=DateTimeOffset.Now.ToString("o"),Window=window.ToInt64(),Show=placement.Show,PlacementFlags=placement.Flags,Minimized=IsIconic(window),Maximized=IsZoomed(window),Left=bounds.Left,Top=bounds.Top,Right=bounds.Right,Bottom=bounds.Bottom};}
        static void SettlePresentation(IntPtr window,Presentation prior,CodexPresentationTrace trace){if(prior==null)return;var watch=Stopwatch.StartNew();for(int i=0;watch.ElapsedMilliseconds<2000;i++){PreservePresentation(window,prior);if(i%4==0)trace.Samples.Add(SamplePresentation("settling",window));Thread.Sleep(50);}PreservePresentation(window,prior);var final=SamplePresentation("settled",window);trace.Samples.Add(final);if(prior.Placement.Show==3&&!final.Maximized)throw new InvalidOperationException("Codex尚未保持最大化状态。");}
        internal static CodexLaunchResult Activate(IntPtr window){
            if(window==IntPtr.Zero)throw new InvalidOperationException("Codex 窗口尚未出现。");
            StopFlash(window);
            // Already-visible windows keep their maximized/fullscreen presentation.
            // Only a window that was hidden or minimized needs a show command.
            if(IsIconic(window)||!IsWindowVisible(window)){uint owner;int show=9;if(!IsIconic(window))show=5;else{var placement=new Placement {Length=(uint)Marshal.SizeOf(typeof(Placement))};if(GetWindowPlacement(window,ref placement)&&(placement.Flags&2)!=0)show=3;}if(GetWindowThreadProcessId(window,out owner)==GetCurrentThreadId())ShowWindow(window,show);else ShowWindowAsync(window,show);}
            // Restore is asynchronous across processes; wait before requesting foreground.
            for(int i=0;i<20&&IsIconic(window);i++)Thread.Sleep(25);
            var prior=GetForegroundWindow();
            // Raise without joining input queues or sending any keyboard event.
            bool topmost=(GetWindowLong(window,-20)&8)!=0;
            if(!SetWindowPos(window,new IntPtr(-1),0,0,0,0,0x53))throw new InvalidOperationException("Codex窗口顺序调整失败。");
            if(!topmost&&!SetWindowPos(window,new IntPtr(-2),0,0,0,0,0x53))throw new InvalidOperationException("Codex临时置顶状态未能撤销。");
            // Raising is sufficient for this action. A rejected foreground request
            // would flash the taskbar; do not issue it or synthesize keyboard input.
            StopFlash(window);
            RequireVisible(IsWindowVisible(window),IsIconic(window));
            return new CodexLaunchResult {Visible=true,Raised=IsAbove(window,prior),Foreground=IsTargetForeground(window),Window=window.ToInt64(),ForegroundWindow=GetForegroundWindow().ToInt64()};
        }
        public static CodexLaunchResult Run(){
            var trace=new CodexPresentationTrace {Started=DateTimeOffset.Now.ToString("o")};try{
            var window=FindWindow();
            var presentation=window==IntPtr.Zero?null:RememberPresentation(window);
            if(window!=IntPtr.Zero)trace.Samples.Add(SamplePresentation("before-activation",window));
            uint activated=0;
            if(window!=IntPtr.Zero){
                if(IsWindowVisible(window)&&!IsIconic(window)&&IsTargetForeground(window)){trace.Route="already-foreground-no-change";trace.Samples.Add(SamplePresentation("unchanged",window));trace.Success=true;return ExistingResult(window);}
                bool focused=false;try{focused=FocusExistingWindow(window,presentation);}catch(TargetInvocationException ex){trace.DirectFocusError=(ex.InnerException??ex).Message;}catch(IOException ex){trace.DirectFocusError=ex.Message;}catch(NotSupportedException ex){trace.DirectFocusError=ex.Message;}
                if(focused){trace.Route="existing-window-pattern";trace.Samples.Add(SamplePresentation("after-direct-window-focus",window));PreservePresentation(window,presentation);Thread.Sleep(200);trace.Samples.Add(SamplePresentation("direct-window-stable",window));var direct=ExistingResult(window);if(direct.Foreground){StopFlash(window);trace.Success=true;return direct;}}
                trace.Route="package-reactivation-fallback";
            }else trace.Route="cold-start";
            if(window!=IntPtr.Zero){var appId=RunningApplicationId(window);if(appId!=null){StopFlash(window);activated=ActivateApplication(appId);for(int i=0;i<20&&!IsTargetForeground(window);i++)Thread.Sleep(50);}}
            if(window==IntPtr.Zero){
                var target=CodexDiscovery.Resolve();
                if(target.Kind=="package"){
                    activated=ActivateApplication(target.Value);
                }else using(var process=Process.Start(new ProcessStartInfo(target.Value){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(target.Value)})){activated=(uint)process.Id;}
            }
            for(int i=0;i<80&&window==IntPtr.Zero;i++){Thread.Sleep(100);window=FindWindow();}
            trace.Samples.Add(SamplePresentation("after-application-activation",window));PreservePresentation(window,presentation);var result=Activate(window);SettlePresentation(window,presentation,trace);result.Foreground=IsTargetForeground(window);result.ForegroundWindow=GetForegroundWindow().ToInt64();if(activated==0)GetWindowThreadProcessId(window,out activated);result.ActivatedProcess=activated;if(!result.Foreground)throw new InvalidOperationException("Codex窗口已恢复，但没有切到前台。");trace.Success=true;return result;
            }catch(Exception ex){trace.Success=false;trace.Error=ex.Message;throw;}finally{try{Data.Save(Path.Combine(Data.Dir,"codex-window-last.json"),trace);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
        [STAThread] public static int Main(string[] args){
            try{
                if(args.Contains("--check-discovery")){
                    int count=0;CodexDiscovery.Checks((ok,name)=>{if(!ok)throw new Exception(name);count++;Console.WriteLine(name);});
                    Console.WriteLine("运行应用："+CodexDiscovery.Resolve());Console.WriteLine("安装登记："+CodexDiscovery.Resolve(false));Console.WriteLine("自动识别检查通过："+count);return 0;
                }
                if(args.Contains("--check-presentation")){
                    Application.EnableVisualStyles();using(var form=new Form {Text="窗口状态保持检查",Width=650,Height=420}){
                        form.Show();Application.DoEvents();var normal=RememberPresentation(form.Handle);Activate(form.Handle);var after=RememberPresentation(form.Handle);if(after.Placement.Show!=normal.Placement.Show||!after.Bounds.Equals(normal.Bounds))throw new Exception("普通窗口状态改变");
                        form.WindowState=FormWindowState.Maximized;Application.DoEvents();var maximized=RememberPresentation(form.Handle);Activate(form.Handle);after=RememberPresentation(form.Handle);if(after.Placement.Show!=maximized.Placement.Show||!after.Bounds.Equals(maximized.Bounds))throw new Exception("最大化窗口状态改变");
                        var staleMaximized=new Presentation {Placement=maximized.Placement,Bounds=maximized.Bounds};staleMaximized.Bounds.Right-=80;staleMaximized.Bounds.Bottom-=80;PreservePresentation(form.Handle,staleMaximized);Application.DoEvents();after=RememberPresentation(form.Handle);if(!after.Bounds.Equals(maximized.Bounds))throw new Exception("最大化窗口被旧尺寸缩小");
                        form.WindowState=FormWindowState.Normal;Application.DoEvents();PreservePresentation(form.Handle,maximized);Application.DoEvents();after=RememberPresentation(form.Handle);if(after.Placement.Show!=maximized.Placement.Show||!after.Bounds.Equals(maximized.Bounds))throw new Exception("最大化状态恢复失败");
                        form.WindowState=FormWindowState.Minimized;Application.DoEvents();var minimizedMax=RememberPresentation(form.Handle);if(minimizedMax==null||minimizedMax.Placement.Show!=3)throw new Exception("最小化前最大化状态没有保存");PreservePresentation(form.Handle,minimizedMax);Application.DoEvents();if(!IsZoomed(form.Handle))throw new Exception("最小化后未恢复最大化");
                        form.WindowState=FormWindowState.Normal;form.FormBorderStyle=FormBorderStyle.None;form.Bounds=Screen.FromHandle(form.Handle).Bounds;Application.DoEvents();var fullscreen=RememberPresentation(form.Handle);Activate(form.Handle);after=RememberPresentation(form.Handle);if(!after.Bounds.Equals(fullscreen.Bounds))throw new Exception("无边框全屏尺寸改变");form.Bounds=new System.Drawing.Rectangle(150,150,650,420);Application.DoEvents();PreservePresentation(form.Handle,fullscreen);Application.DoEvents();after=RememberPresentation(form.Handle);if(!after.Bounds.Equals(fullscreen.Bounds))throw new Exception("无边框全屏尺寸恢复失败");form.Close();
                    }Console.WriteLine("普通、最大化及无边框全屏保持/恢复检查通过；没有操作真实Codex窗口。");return 0;
                }
                if(args.Contains("--check")){
                    RequireVisible(true,false);var pending=new CodexLaunchResult {Visible=true,Foreground=false};if(!pending.Description.Contains("尚未成功"))throw new Exception("焦点未确认必须保留明确提醒");
                    bool rejected=false;try{RequireVisible(true,true);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("恢复失败必须拒绝");
                    rejected=false;try{RequireVisible(false,false);}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("隐藏窗口必须拒绝");
                    Application.EnableVisualStyles();using(var form=new Form {Text="Codex窗口恢复检查"}){form.Show();Application.DoEvents();form.WindowState=FormWindowState.Minimized;Application.DoEvents();Activate(form.Handle);Thread.Sleep(100);Application.DoEvents();if(form.WindowState==FormWindowState.Minimized||GetForegroundWindow()!=form.Handle)throw new Exception("最小化窗口恢复检查失败");form.Close();}
                    Console.WriteLine("本机路径："+Resolve());Console.WriteLine("窗口恢复及前台检查通过；未订阅鼠标、未执行快捷键。");return 0;
                }
                if(args.Contains("--activate-package")){var registered=CodexDiscovery.Resolve(false);if(registered.Kind!="package")throw new Exception("本机不是包安装");using(var p=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),registered.ToString()){UseShellExecute=true})){}Thread.Sleep(1200);}
                if(args.Contains("--check-codex-minimized")){var window=FindWindow();if(window==IntPtr.Zero)throw new Exception("没有已打开的 Codex 窗口");ShowWindowAsync(window,6);Thread.Sleep(200);Run();if(IsIconic(window))throw new Exception("真实 Codex 仍最小化");}
                if(args.Contains("--check-background")){
                    Application.EnableVisualStyles();using(var blocker=new Form {Text="Codex后台激活验证",Width=700,Height=450}){
                        blocker.Show();Application.DoEvents();SetForegroundWindow(blocker.Handle);Application.DoEvents();
                        var before=GetForegroundWindow();var target=FindWindow();if(before==target)throw new Exception("前台原本就是Codex，不能当后台测试");bool wasTopmost=(GetWindowLong(target,-20)&8)!=0;
                        var backgroundResult=Run();Console.WriteLine("验证前台="+before+"，激活进程="+backgroundResult.ActivatedProcess+"，"+backgroundResult.Description);
                        if(!backgroundResult.Foreground&&!IsAbove(new IntPtr(backgroundResult.Window),before))throw new Exception("窗口未提到原前台上方，检查未通过");blocker.Close();
                        if(((GetWindowLong(new IntPtr(backgroundResult.Window),-20)&8)!=0)!=wasTopmost)throw new Exception("窗口置顶状态没有恢复");
                    }return 0;
                }
                var result=Run();Console.WriteLine(result.Description);return 0;
            }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
        }
    }
}
