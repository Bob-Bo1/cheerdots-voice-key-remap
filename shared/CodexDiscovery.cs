using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CheerCustom {
    sealed class CodexTarget {
        public string Kind,Value;
        public override string ToString(){return Kind=="package"?"shell:AppsFolder\\"+Value:Value;}
    }
    static class CodexDiscovery {
        internal static bool DesktopIdentity(string product,string company){return String.Equals((product??"").Trim(),"Codex",StringComparison.OrdinalIgnoreCase)&&(company??"").IndexOf("OpenAI",StringComparison.OrdinalIgnoreCase)>=0;}
        internal static bool DesktopExecutable(string path){
            try{if(String.IsNullOrEmpty(path)||!Path.IsPathRooted(path)||!File.Exists(path)||!String.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase))return false;var info=FileVersionInfo.GetVersionInfo(path);return DesktopIdentity(info.ProductName,info.CompanyName);}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}
        }
        internal static IEnumerable<CodexTarget> Running(){
            var found=new List<CodexTarget>();
            foreach(var p in Process.GetProcesses())using(p){try{if(p.MainWindowHandle!=IntPtr.Zero){var path=p.MainModule.FileName;if(DesktopExecutable(path))found.Add(new CodexTarget {Kind="exe",Value=path});}}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}}
            return found;
        }
        static object Invoke(object value,string name,params object[] args){return value.GetType().InvokeMember(name,BindingFlags.InvokeMethod|BindingFlags.GetProperty,null,value,args);}
        static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        internal static bool PackageIdentity(string id){return !String.IsNullOrEmpty(id)&&id.StartsWith("OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)&&id.IndexOf('!')>"OpenAI.Codex_".Length&&id.IndexOf('!')==id.LastIndexOf('!')&&id.IndexOfAny(new[]{'\\','/','"','\r','\n'})<0;}
        static IEnumerable<CodexTarget> Packages(){
            var found=new List<CodexTarget>();object shell=null,folder=null,items=null;
            try{
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application",true));folder=Invoke(shell,"Namespace","shell:AppsFolder");if(folder==null)return found;items=Invoke(folder,"Items");
                foreach(object item in (IEnumerable)items)try{var id=Convert.ToString(Invoke(item,"ExtendedProperty","System.AppUserModel.ID"));if(PackageIdentity(id))found.Add(new CodexTarget {Kind="package",Value=id});}finally{Release(item);}
            }catch(COMException){}catch(TargetInvocationException){}
            finally{Release(items);Release(folder);Release(shell);}return found;
        }
        static IEnumerable<string> RegistryPaths(){
            var paths=new List<string>();
            foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{
                using(var root=RegistryKey.OpenBaseKey(hive,view)){
                    foreach(var name in new[]{"Codex.exe","ChatGPT.exe"})using(var app=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+name)){if(app!=null)paths.Add(Convert.ToString(app.GetValue("")));}
                    using(var uninstall=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))if(uninstall!=null)foreach(var name in uninstall.GetSubKeyNames())using(var app=uninstall.OpenSubKey(name)){
                        if(app==null||!String.Equals(Convert.ToString(app.GetValue("DisplayName")),"Codex",StringComparison.OrdinalIgnoreCase))continue;
                        var location=Convert.ToString(app.GetValue("InstallLocation"));if(!String.IsNullOrEmpty(location))foreach(var relative in new[]{"Codex.exe","ChatGPT.exe",@"app\ChatGPT.exe"})paths.Add(Path.Combine(location,relative));
                        var icon=Convert.ToString(app.GetValue("DisplayIcon"));if(!String.IsNullOrEmpty(icon)){var end=icon.LastIndexOf(',');paths.Add((end>0?icon.Substring(0,end):icon).Trim().Trim('"'));}
                    }
                }
            }catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}
            return paths;
        }
        static IEnumerable<string> Shortcuts(){
            var paths=new List<string>();object shell=null;
            try{
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
                foreach(var folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)}){
                    string[] links;try{links=Directory.GetFiles(folder,"*.lnk",SearchOption.AllDirectories);}catch(IOException){continue;}catch(UnauthorizedAccessException){continue;}
                    foreach(var link in links){var name=Path.GetFileNameWithoutExtension(link);if(!name.StartsWith("Codex",StringComparison.OrdinalIgnoreCase)&&!String.Equals(name,"ChatGPT",StringComparison.OrdinalIgnoreCase))continue;object shortcut=null;try{shortcut=Invoke(shell,"CreateShortcut",link);paths.Add(Convert.ToString(Invoke(shortcut,"TargetPath")));}catch(COMException){}catch(TargetInvocationException){}finally{Release(shortcut);}}
                }
            }catch(COMException){}catch(TargetInvocationException){}finally{Release(shell);}return paths;
        }
        static IEnumerable<string> KnownPaths(){
            var paths=new List<string>();
            foreach(var folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(';').Concat(new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})){
                if(String.IsNullOrWhiteSpace(folder))continue;try{foreach(var relative in new[]{"Codex.exe","ChatGPT.exe",@"Codex\Codex.exe",@"Codex\ChatGPT.exe",@"Programs\Codex\Codex.exe",@"Programs\Codex\ChatGPT.exe"})paths.Add(Path.Combine(folder.Trim().Trim('"'),relative));}catch(ArgumentException){}
            }return paths;
        }
        internal static CodexTarget Unique(IEnumerable<CodexTarget> targets){
            var unique=targets.GroupBy(p=>p.Kind+"|"+p.Value,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
            if(unique.Count>1)throw new InvalidOperationException("检测到多个 Codex 桌面安装，请先打开要使用的那个，再重试。");return unique.FirstOrDefault();
        }
        public static CodexTarget Resolve(bool includeRunning=true){
            var running=includeRunning?Unique(Running()):null;if(running!=null)return running;
            var package=Unique(Packages());if(package!=null)return package;
            var registered=Unique(RegistryPaths().Concat(Shortcuts()).Where(DesktopExecutable).Select(path=>new CodexTarget {Kind="exe",Value=path}));if(registered!=null)return registered;
            var common=Unique(KnownPaths().Where(DesktopExecutable).Select(path=>new CodexTarget {Kind="exe",Value=path}));if(common!=null)return common;
            throw new FileNotFoundException("没有找到 Codex 桌面应用。请先安装或手动打开一次；命令行 Codex 不用于此功能。");
        }
        internal static void Checks(Action<bool,string> check){
            check(DesktopIdentity("Codex","OpenAI OpCo, LLC"),"桌面产品身份通过");check(!DesktopIdentity("","OpenAI")&&!DesktopIdentity("ChatGPT","OpenAI")&&!DesktopIdentity("Codex","Other"),"命令行/Classic/其他厂商不能冒充桌面");
            check(PackageIdentity("OpenAI.Codex_newpublisher!Desktop")&&!PackageIdentity("OpenAI.ChatGPT-Desktop_pub!App")&&!PackageIdentity("Other.Codex_pub!App"),"应用标识动态识别，不绑定旧发布者或App后缀");
            check(Unique(new[]{new CodexTarget {Kind="exe",Value=@"E:\Tools\Codex\ChatGPT.exe"}}).Value.StartsWith("E:"),"不同盘符路径不改配置");
            check(Unique(new[]{new CodexTarget {Kind="package",Value="OpenAI.Codex_newpublisher!Desktop"}}).Value.EndsWith("!Desktop"),"包登记变化无需旧固定标识");
            check(Unique(new CodexTarget[0])==null,"未安装时不猜程序路径");bool rejected=false;try{Unique(new[]{new CodexTarget {Kind="exe",Value=@"D:\a.exe"},new CodexTarget {Kind="exe",Value=@"E:\b.exe"}});}catch(InvalidOperationException){rejected=true;}check(rejected,"多个安装不静默猜测");
            check(Unique(new[]{new CodexTarget {Kind="exe",Value=@"E:\Codex.exe"},new CodexTarget {Kind="exe",Value=@"e:\codex.exe"}})!=null,"重复登记去重");
        }
    }
}
