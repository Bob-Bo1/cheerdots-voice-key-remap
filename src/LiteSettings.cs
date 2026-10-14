using System;
using System.IO;
using CheerCustom;

namespace CheerVoiceLite {
    public sealed class LiteSettings {
        public int Version=2;
        public LiteConfig Single=new LiteConfig(),Double=new LiteConfig {Action="未设置"};
        internal static LiteSettings Load(string path){
            if(!File.Exists(path))return new LiteSettings();
            var version=Data.Read<LiteConfig>(path).Version;
            var settings=version==1?new LiteSettings {Single=Data.Read<LiteConfig>(path)}:Data.Read<LiteSettings>(path);
            settings.Validate();return settings;
        }
        internal void Validate(){
            if(Version!=2)throw new InvalidDataException("配置版本不支持，原文件保留。");
            foreach(var item in new[]{Single,Double}){
                if(item==null||item.Version!=1||Array.IndexOf(LiteWindow.Kinds,item.Action)<0||item.Value==null)throw new InvalidDataException("动作配置不完整。");
                if(item.Action!="快捷键"&&item.Action!="打开应用程序"&&item.Value!="")throw new InvalidDataException("这个动作不需要填写内容。");
            }
        }
        internal LiteConfig ForCode(int code){return code==0x41?Single:code==0x42?Double:null;}
        internal void Save(string path){
            Validate();
            if(File.Exists(path)&&Data.Read<LiteConfig>(path).Version==1&&!File.Exists(path+".before-dual.json"))File.Copy(path,path+".before-dual.json");
            Data.Save(path,this);
        }
    }
}
