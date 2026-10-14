using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using CheerCustom;

namespace CheerVoiceLite {
    public sealed class StartupPreferences {public bool Enabled;public string Address="",Container="";}
    sealed class StartupController {
        internal const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run",ValueName="VoiceKeyRemapTool";
        readonly string file,executable,registryPath,valueName;
        internal StartupPreferences Preferences {get;private set;}
        internal StartupController(string path,string exe,string key=RunKey,string name=ValueName){file=path;executable=Path.GetFullPath(exe);registryPath=key;valueName=name;Preferences=File.Exists(file)?Data.Read<StartupPreferences>(file):new StartupPreferences();if(Preferences==null)throw new InvalidDataException("自启配置无法读取。");}
        internal string Command {get{return "\""+executable+"\" --startup";}}
        internal bool Enabled {get{using(var key=Registry.CurrentUser.OpenSubKey(registryPath))return Preferences.Enabled&&key!=null&&String.Equals(key.GetValue(valueName) as string,Command,StringComparison.OrdinalIgnoreCase);}}
        internal void SetEnabled(bool enabled,MouseCandidate target){
            var next=new StartupPreferences {Enabled=enabled,Address=target==null?Preferences.Address:target.Address,Container=target==null?Preferences.Container:target.Container};
            using(var key=Registry.CurrentUser.CreateSubKey(registryPath)){
                var previous=key.GetValue(valueName,null);try{if(enabled)key.SetValue(valueName,Command,RegistryValueKind.String);else key.DeleteValue(valueName,false);Data.Save(file,next);Preferences=next;}
                catch{if(previous==null)key.DeleteValue(valueName,false);else key.SetValue(valueName,previous);throw;}
            }
        }
        internal void Remember(MouseCandidate target){if(!Preferences.Enabled||target==null)return;var next=new StartupPreferences {Enabled=true,Address=target.Address,Container=target.Container};if(next.Address==Preferences.Address&&next.Container==Preferences.Container)return;Data.Save(file,next);Preferences=next;}
        internal MouseCandidate Pick(MouseCandidate[] candidates){
            var healthy=candidates.Where(x=>TargetDiscovery.Check(x)=="").ToArray();
            if(!String.IsNullOrEmpty(Preferences.Address))healthy=healthy.Where(x=>x.Address==Preferences.Address&&String.Equals(x.Container,Preferences.Container,StringComparison.OrdinalIgnoreCase)).ToArray();
            if(healthy.Length!=1)throw new InvalidOperationException(healthy.Length==0?"等待原鼠标连接":"发现多只鼠标，请打开设置选择");return healthy[0];
        }
    }
}
