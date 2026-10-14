using System;
using System.IO;
using System.Collections.Generic;
using CheerCustom;

namespace CheerVoiceLite {
    static class DualFeatureChecks {
        internal static string[] Run(string folder){
            Directory.CreateDirectory(folder);var path=Path.Combine(folder,"legacy.json");Data.Save(path,new LiteConfig {Action="收回 Codex"});
            var before=File.ReadAllText(path);var migrated=LiteSettings.Load(path);if(File.ReadAllText(path)!=before||migrated.Single.Action!="收回 Codex"||migrated.Double.Action!="未设置")throw new Exception("旧配置迁移改变原动作或写回原文件");
            migrated.Double=new LiteConfig {Action="快捷键",Value="Ctrl+"};migrated.Save(path);if(LiteSettings.Load(path).Double.Value!="Ctrl+"||File.ReadAllText(path+".before-dual.json")!=before)throw new Exception("草稿或迁移备份未保留");
            migrated.Double=new LiteConfig {Action="收回 Codex"};migrated.Save(path);var loaded=LiteSettings.Load(path);if(loaded.Double.Action!="收回 Codex"||loaded.Single.Action!="收回 Codex"||loaded.ForCode(0x21)!=null)throw new Exception("单双击配置保存或操作区分错误");
            Data.Save(Path.Combine(folder,"future.json"),new LiteSettings {Version=99});bool rejected=false;try{LiteSettings.Load(Path.Combine(folder,"future.json"));}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("未来版本不应覆盖读取");
            var results=new List<string>{"旧单击配置只读迁移，双击默认未设置；首次保存保留独立旧版备份","两项配置保存重读及未完成输入草稿保留；未知配置版本拒绝"};
            results.AddRange(LiveLogSession.Checks(Path.Combine(folder,"log")));return results.ToArray();
        }
    }
}
