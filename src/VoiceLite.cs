using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CheerCustom;
using Shortcut=CheerCustom.Shortcut;

namespace CheerVoiceLite {
    public sealed class LiteConfig {public int Version=1;public string Action="打开 Codex",Value="";}
    public sealed class LiteRunRecord {public string Time,Message;}
    static class LiteActions {
        static object Member(object o,string name,params object[] args){return o.GetType().InvokeMember(name,BindingFlags.InvokeMethod|BindingFlags.GetProperty,null,o,args);}
        internal static string ShortcutTarget(string path){
            object shell=null,link=null;try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));link=Member(shell,"CreateShortcut",path);return Convert.ToString(Member(link,"TargetPath"));}
            finally{if(link!=null)Marshal.ReleaseComObject(link);if(shell!=null)Marshal.ReleaseComObject(shell);}
        }
        internal static void Validate(LiteConfig c){
            if(c.Version!=1)throw new InvalidDataException("配置版本不支持，原文件已保留。");
            if(c.Action=="快捷键")Shortcut.Parse(c.Value);
            else if(c.Action=="打开应用程序"){
                if(!Path.IsPathRooted(c.Value)||!File.Exists(c.Value))throw new ArgumentException("请选择存在的程序或快捷方式。");
                var extension=Path.GetExtension(c.Value).ToLowerInvariant();
                if(extension==".lnk"){var target=ShortcutTarget(c.Value);if(!File.Exists(target)||!String.Equals(Path.GetExtension(target),".exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("快捷方式需要指向有效的EXE程序。");}
                else if(extension!=".exe")throw new ArgumentException("仅接受EXE程序或LNK快捷方式。");
            }else if(c.Action!="打开 Codex"&&c.Action!="收回 Codex"&&c.Action!="打开微信"&&c.Action!="未设置")throw new ArgumentException("未知功能类型。");
            if(c.Action!="快捷键"&&c.Action!="打开应用程序"&&!String.IsNullOrEmpty(c.Value))throw new ArgumentException("这个功能不需要填写地址。");
        }
        static bool WechatExe(string path){try{var info=FileVersionInfo.GetVersionInfo(path);return new[]{"Weixin","WeChat","微信"}.Contains(info.ProductName)&&(info.CompanyName??"").IndexOf("Tencent",StringComparison.OrdinalIgnoreCase)>=0;}catch{return false;}}
        internal static string Wechat(){
            foreach(var process in Process.GetProcesses())using(process)try{if(process.MainWindowHandle!=IntPtr.Zero&&WechatExe(process.MainModule.FileName))return process.MainModule.FileName;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
            var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")};
            foreach(var root in roots)foreach(var suffix in new[]{@"Tencent\Weixin\Weixin.exe",@"Tencent\WeChat\WeChat.exe",@"Weixin\Weixin.exe"}){var path=Path.Combine(root,suffix);if(File.Exists(path)&&WechatExe(path))return path;}
            var links=new[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)};
            foreach(var folder in links){string[] files;try{files=Directory.GetFiles(folder,"*.lnk",SearchOption.AllDirectories);}catch(IOException){continue;}catch(UnauthorizedAccessException){continue;}
                foreach(var file in files)if(new[]{"微信","WeChat","Weixin"}.Contains(Path.GetFileNameWithoutExtension(file))){var target=ShortcutTarget(file);if(File.Exists(target)&&WechatExe(target))return target;}}
            throw new FileNotFoundException("没有找到微信，请先安装或手动打开一次微信。");
        }
        internal static string Run(LiteConfig c){Validate(c);if(c.Action=="未设置")return "未设置，没有执行动作。";
            if(c.Action=="打开 Codex")return CodexLauncher.Run().Description;
            if(c.Action=="收回 Codex")return CodexLauncher.Minimize();
            if(c.Action=="打开微信"){var path=Wechat();using(var p=Process.Start(new ProcessStartInfo(path){UseShellExecute=true})){}return "已请求打开微信；请确认窗口效果。";}
            if(c.Action=="快捷键"){Shortcut.Parse(c.Value).Send();return "已发送快捷键；请确认目标程序效果。";}
            using(var p=Process.Start(new ProcessStartInfo(c.Value){UseShellExecute=true})){}return "已请求打开程序；请确认窗口效果。";
        }
    }
    static class LiteEntry {
        [STAThread] public static int Main(string[] args){
            Data.Root=AppDomain.CurrentDomain.BaseDirectory;Data.Dir=Path.Combine(Data.Root,"user-data");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Contains("--startup-check")){try{foreach(var check in StartupChecks.Run(Path.Combine(Data.Dir,"checks","startup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"))))Console.WriteLine(check);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
            if(args.Contains("--dual-check")){try{var checks=DualFeatureChecks.Run(Path.Combine(Data.Dir,"checks","dual-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));foreach(var check in checks)Console.WriteLine(check);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
            if(args.Contains("--log-check")){try{var checks=OfficialLogReader.Checks(Path.Combine(Data.Dir,"checks","log-tail-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")),"A1B2C3D4E5F6").Concat(LogActionQueue.Checks()).ToArray();Data.Save(Path.Combine(Data.Dir,"checks","log-action-results.json"),checks);foreach(var item in checks)Console.WriteLine(item);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
            if(args.Contains("--check")){try{var config=new LiteConfig();LiteActions.Validate(config);bool rejected=false;try{LiteActions.Validate(new LiteConfig {Action="打开应用程序",Value="D:/missing.ps1"});}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("无效程序没有拒绝");var path=Path.Combine(Data.Dir,"checks","config.json");Data.Save(path,config);if(Data.Read<LiteConfig>(path).Action!="打开 Codex")throw new Exception("配置保存重读失败");LiteActions.Validate(new LiteConfig {Action="快捷键",Value="Ctrl+Shift+A"});using(var ui=new LiteWindow(Path.Combine(Data.Dir,"checks","ui-profile.json")))ui.Exercise();Console.WriteLine("独立配置、动作字段、快捷键、无效路径、日志容量与托盘检查通过。");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
            bool preview=args.Contains("--preview"),startup=args.Contains("--startup"),created;using(var mutex=new Mutex(!preview,"Local\\CheerdotsWechatVoice",out created)){if(!created&&!preview){if(!startup)MessageBox.Show("语音改建工具已在运行，请从托盘打开设置。",AppBrand.Name);return startup?0:1;}using(var window=new LiteWindow()){if(startup&&!window.StartSilent())return 0;if(args.Contains("--pin")&&!startup)window.PinForTest();if(preview){Directory.CreateDirectory(Data.Dir);window.Preview(Path.Combine(Data.Dir,"preview.png"));}else Application.Run(window);}}return 0;
        }
    }
}
