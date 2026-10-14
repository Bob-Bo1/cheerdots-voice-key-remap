using System;using System.Runtime.InteropServices;using System.Windows.Forms;
namespace CheerCustom {
    sealed class VoiceShortcutSequence {
        public uint PendingModifiers {get;private set;}
        public void Clear(){PendingModifiers=0;}
        internal static bool IsIntermediate(int key){return key==0||key==(int)Keys.ProcessKey||key==(int)Keys.Packet;}
        public bool Capture(Keys keyData,out string text){
            text=null;int key=(int)(keyData&Keys.KeyCode);
            if(IsIntermediate(key))return true;
            if(key==(int)Keys.Escape&&PendingModifiers!=0){Clear();return true;}
            uint modifiers=((keyData&Keys.Control)!=0?2u:0u)|((keyData&Keys.Alt)!=0?1u:0u)|((keyData&Keys.Shift)!=0?4u:0u);
            if(Shortcut.IsModifier(key)){PendingModifiers|=Shortcut.Mods(new[]{key});return true;}
            modifiers|=PendingModifiers;if(modifiers==0)return false;
            text=Shortcut.Parse(Shortcut.Format(key,modifiers)).ToString();Clear();return true;
        }
        internal static void Checks(Action<bool,string> check){
            var sequence=new VoiceShortcutSequence();string text;
            check(sequence.Capture(Keys.Menu,out text)&&text==null&&sequence.PendingModifiers==1,"分步按Alt只记录修饰键，不填写主键或执行功能");
            check(sequence.Capture(Keys.A,out text)&&text=="Alt+A"&&sequence.PendingModifiers==0,"Alt松开后单独A组合为Alt+A，完成后清空待组合状态");
            sequence.Capture(Keys.ControlKey,out text);check(sequence.Capture(Keys.A,out text)&&text=="Ctrl+A","Ctrl松开后单独A组合为Ctrl+A，无需实际同时按截图组合键");
            sequence.Capture(Keys.ControlKey,out text);sequence.Capture(Keys.ShiftKey,out text);check(sequence.Capture(Keys.C,out text)&&text=="Ctrl+Shift+C","多个修饰键可逐个按下松开后与主键组合");
            check(sequence.Capture(Keys.Control|Keys.C,out text)&&text=="Ctrl+C","同时按组合键仍识别，原录入方式保留");
            sequence.Capture(Keys.Menu,out text);sequence.Clear();check(!sequence.Capture(Keys.A,out text)&&text==null,"失焦清空后普通字符仍可手动输入，不残留Alt");
            sequence.Capture(Keys.ControlKey,out text);check(sequence.Capture(Keys.Escape,out text)&&sequence.PendingModifiers==0&&!sequence.Capture(Keys.A,out text),"Esc取消待组合状态，下一次字符不继承修饰键");
            sequence.Capture(Keys.ControlKey,out text);check(sequence.Capture(Keys.ProcessKey,out text)&&text==null&&sequence.PendingModifiers==2,"未知输入法消息不生成ProcessKey快捷键，保留待组合Ctrl");
            check(sequence.Capture(Keys.Packet,out text)&&text==null&&sequence.PendingModifiers==2,"输入法文本包不作为快捷键主键");
            check(sequence.Capture(Keys.A,out text)&&text=="Ctrl+A","输入法中间消息之后实际A仍得到Ctrl+A");
            sequence.Capture(Keys.ControlKey|Keys.Alt,out text);check(sequence.PendingModifiers==2,"修饰键按其自身键值记录，不把Ctrl消息附带的Alt标记累积进去");sequence.Clear();
        }
    }
static class VoiceClickEditor {
[DllImport("imm32.dll")]static extern uint ImmGetVirtualKey(IntPtr window);
[DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint MapVirtualKeyW(uint code,uint type);
        internal static Keys NormalizeKey(ref Message msg,Keys keyData){
            if((msg.Msg==0x100||msg.Msg==0x104)&&(keyData&Keys.KeyCode)==Keys.ProcessKey){
                uint original=ImmGetVirtualKey(msg.HWnd);
                if(VoiceShortcutSequence.IsIntermediate((int)original)){
                    long bits=msg.LParam.ToInt64();uint scan=(uint)((bits>>16)&255);
                    if((bits&0x01000000)!=0)scan|=0xe000;
                    original=scan==0?0:MapVirtualKeyW(scan,3);
                }
                if(!VoiceShortcutSequence.IsIntermediate((int)original))keyData=(keyData&Keys.Modifiers)|(Keys)original;
            }
            return keyData;
        }
}}