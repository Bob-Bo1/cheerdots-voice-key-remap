using System;
using System.IO;
using Microsoft.Win32;
using CheerCustom;

namespace CheerVoiceLite {
    static class StartupChecks {
        internal static string[] Run(string folder){
            Directory.CreateDirectory(folder);string key=@"Software\VoiceKeyRemapTool\SelfTest-"+Guid.NewGuid().ToString("N"),file=Path.Combine(folder,"startup.json");
            try{
                var settings=new StartupController(file,@"D:\应用 示例\语音改建工具.exe",key,"SelfTest");if(settings.Enabled)throw new Exception("新安装不应默认注册自启");
                var target=new MouseCandidate {Address="A1B2C3D4E5F6",Container="11111111-2222-3333-4444-555555555555",Instance=TargetDiscovery.Model+"A1B2C3D4E5F6&COL02\\example",Class="4d36e96f-e325-11ce-bfc1-08002be10318",Service="mouhid",Healthy=true,MouseMatches=1,VoiceMatches=1,VoicePath="test"};
                settings.SetEnabled(true,target);if(!settings.Enabled||settings.Command!="\"D:\\应用 示例\\语音改建工具.exe\" --startup")throw new Exception("自启命令引号/参数不正确");
                var loaded=new StartupController(file,@"D:\应用 示例\语音改建工具.exe",key,"SelfTest");if(!loaded.Enabled||loaded.Pick(new[]{target})!=target)throw new Exception("自启设置或目标未恢复");
                var other=new MouseCandidate {Address="AAAAAAAAAAAA",Container=Guid.NewGuid().ToString(),Instance=TargetDiscovery.Model+"AAAAAAAAAAAA&COL02\\example",Class=target.Class,Service=target.Service,Healthy=true,MouseMatches=1,VoiceMatches=1,VoicePath="other"};bool rejected=false;try{loaded.Pick(new[]{other});}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("原鼠标缺失时不应选其他鼠标");
                if(loaded.Pick(new[]{target,other})!=target)throw new Exception("记录的原目标未能隔离");loaded.SetEnabled(false,null);using(var registry=Registry.CurrentUser.OpenSubKey(key))if(registry.GetValue("SelfTest")!=null)throw new Exception("停用未移除自启值");
                var fresh=new StartupController(Path.Combine(folder,"new.json"),@"D:\语音改建工具.exe",key,"SelfTest");rejected=false;try{fresh.Pick(new[]{target,other});}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("首次多鼠标不得静默任选");
            }finally{Registry.CurrentUser.DeleteSubKey(key,false);}
            return new[]{"自启开关注册/撤销、含空格中文路径命令、设置重读通过（隔离HKCU测试键，未写实际Run）","已记目标按地址和容器恢复；缺失等待、无绑定多候选拒绝"};
        }
    }
}
