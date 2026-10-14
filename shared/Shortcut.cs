using System;using System.Collections.Generic;using System.ComponentModel;using System.Linq;using System.Runtime.InteropServices;using System.Windows.Forms;
namespace CheerCustom {
    class Shortcut {
        public uint ModsValue;public int Key;
        public override string ToString(){return Format(Key,ModsValue);}
        public static bool IsModifier(int key){return new[]{16,17,18,91,92,160,161,162,163,164,165}.Contains(key);}
        public static uint Mods(IEnumerable<int> keys){uint mods=0;foreach(int k in keys){if(k==17||k==162||k==163)mods|=2;if(k==16||k==160||k==161)mods|=4;if(k==18||k==164||k==165)mods|=1;if(k==91||k==92)mods|=8;}return mods;}
        public static string Format(int key,uint mods){return ((mods&2)!=0?"Ctrl+":"")+((mods&1)!=0?"Alt+":"")+((mods&4)!=0?"Shift+":"")+((mods&8)!=0?"Win+":"")+((Keys)key).ToString();}
        public static Shortcut Parse(string text){
            uint mods=0;int key=0;
            foreach(string token in (text??"").Split('+')){string t=token.Trim();if(t.Equals("Ctrl",StringComparison.OrdinalIgnoreCase)||t.Equals("Control",StringComparison.OrdinalIgnoreCase))mods|=2;else if(t.Equals("Alt",StringComparison.OrdinalIgnoreCase))mods|=1;else if(t.Equals("Shift",StringComparison.OrdinalIgnoreCase))mods|=4;else if(t.Equals("Win",StringComparison.OrdinalIgnoreCase))mods|=8;else {Keys k;if(t.Length==1&&Char.IsDigit(t[0]))t="D"+t;if(!Enum.TryParse<Keys>(t,true,out k)||IsModifier((int)k)||((int)k<=0)||((int)k>254)||key!=0)throw new ArgumentException("快捷键格式示例：Ctrl+Alt+Shift+F9");key=(int)k;}}
            if(key==0)throw new ArgumentException("快捷键缺少主按键");return new Shortcut{Key=key,ModsValue=mods};
        }
        public void ValidateGlobal(){if(Key==(int)Keys.F12)throw new ArgumentException("F12 被调试器保留，请换一个键");if(ModsValue==0 && (Key<(int)Keys.F13||Key>(int)Keys.F24))throw new ArgumentException("专用热键请使用带 Ctrl/Alt/Shift 的组合，或 F13–F24");if((ModsValue&8)!=0)throw new ArgumentException("Win 组合键由系统保留，专用信号请使用 Ctrl/Alt/Shift");}
        public void Send(){
            var mods=new List<int>();if((ModsValue&2)!=0)mods.Add(17);if((ModsValue&1)!=0)mods.Add(18);if((ModsValue&4)!=0)mods.Add(16);if((ModsValue&8)!=0)mods.Add(91);
            if(new[]{16,17,18,91,92}.Any(k=>(Native.GetAsyncKeyState(k)&0x8000)!=0))throw new InvalidOperationException("请先松开 Ctrl、Alt、Shift、Win，再执行快捷键");
            var seq=new List<Native.Input>();Action<int,bool> add=(k,up)=>seq.Add(new Native.Input{Type=1,Key=new Native.KeyInput{Vk=(ushort)k,Flags=up?2u:0u,Extra=new UIntPtr(0x43485244)}});
            foreach(int m in mods)add(m,false);add(Key,false);add(Key,true);mods.Reverse();foreach(int m in mods)add(m,true);
            uint n=Native.SendInput((uint)seq.Count,seq.ToArray(),40);
            if(n!=seq.Count){var releases=new List<Native.Input>();foreach(var item in seq.Take((int)n).Where(x=>x.Key.Flags==0))releases.Add(new Native.Input{Type=1,Key=new Native.KeyInput{Vk=item.Key.Vk,Flags=2,Extra=new UIntPtr(0x43485244)}});if(releases.Count>0)Native.SendInput((uint)releases.Count,releases.ToArray(),40);throw new Win32Exception(Marshal.GetLastWin32Error(),"快捷键未完整发送；可能受窗口权限限制");}
        }
    }
}