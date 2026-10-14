using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CheerCustom {
    public sealed class MouseCandidate {
        public string Address,Container,Instance,Class,Service,VoicePath,Reason;
        public bool Healthy;
        public int MouseMatches,VoiceMatches;
        public override string ToString(){return "Cheerdots · "+Address+" · "+(Reason==""?"身份核对通过":Reason);}
    }
    // Discovery reads device metadata only. It does not register Raw Input or a hook.
    static class TargetDiscovery {
        internal const string Model="HID\\{00001812-0000-1000-8000-00805F9B34FB}_DEV_VID&0105AC_PID&024F_REV&0003_";
        [DllImport("cfgmgr32.dll")]static extern uint CM_Get_DevNode_Status(out uint status,out uint problem,uint node,uint flags);
        static string Property(uint node,uint property){uint type,length=4096;var bytes=new byte[length];if(Native.CM_Get_DevNode_Registry_PropertyW(node,property,out type,bytes,ref length,0)!=0||type!=1||length<2||length>bytes.Length||length%2!=0)return "";return Encoding.Unicode.GetString(bytes,0,(int)length).TrimEnd('\0');}
        internal static string AddressOf(string instance){
            var match=Regex.Match(instance??"","^"+Regex.Escape(Model)+"([0-9A-F]{12})&COL02\\\\[^\\\\]+$",RegexOptions.IgnoreCase);
            return match.Success?match.Groups[1].Value.ToUpperInvariant():null;
        }
        static string InstanceOf(string path){var key=new Native.PropertyKey(new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"),256);uint type,length=4096;var data=new byte[length];if(VoiceNative.CM_Get_Device_Interface_PropertyW(path,ref key,out type,data,ref length,0)!=0||length<2||length>data.Length||length%2!=0)throw new InvalidDataException("设备实例属性读取失败。");return Encoding.Unicode.GetString(data,0,(int)length).TrimEnd('\0');}
        internal static string Check(MouseCandidate c){
            Guid container;
            if(c==null||AddressOf(c.Instance)!=c.Address||!Guid.TryParse(c.Container,out container)||container==Guid.Empty)return "身份信息不完整";
            if(!String.Equals(c.Class,"4d36e96f-e325-11ce-bfc1-08002be10318",StringComparison.OrdinalIgnoreCase)||c.Service!="mouhid")return "鼠标类别或输入服务不匹配";
            if(c.MouseMatches!=1)return "鼠标接口不唯一";
            if(!c.Healthy)return "设备状态异常";
            if(c.VoiceMatches!=1||String.IsNullOrEmpty(c.VoicePath))return "目标语音服务缺失或不唯一";
            return "";
        }
        internal static MouseCandidate Unique(IEnumerable<MouseCandidate> all){
            var candidates=all.ToArray();
            if(candidates.Length!=1)throw new InvalidOperationException(candidates.Length==0?"没有发现已连接的Cheerdots鼠标。":"发现多只Cheerdots或重复接口，无法自动确定目标。");
            string reason=Check(candidates[0]);if(reason!="")throw new InvalidOperationException(reason);return candidates[0];
        }
        internal static List<MouseCandidate> Scan(){
            uint count=0,size=(uint)Marshal.SizeOf(typeof(Native.DeviceList));if(Native.GetRawInputDeviceList(null,ref count,size)==UInt32.MaxValue||count>1024)throw new Win32Exception("无法读取鼠标设备目录。");
            var raw=new Native.DeviceList[count];if(Native.GetRawInputDeviceList(raw,ref count,size)==UInt32.MaxValue)throw new Win32Exception("鼠标目录读取失败。");
            var found=new List<MouseCandidate>();foreach(var input in raw.Take((int)count).Where(d=>d.Type==0)){
                uint length=0;Native.DeviceInfo(input.Handle,Native.RIDI_NAME,IntPtr.Zero,ref length);if(length<2||length>32768)throw new InvalidDataException("鼠标路径长度异常。");
                IntPtr buffer=Marshal.AllocHGlobal((int)length*2);string path;try{if(Native.DeviceInfo(input.Handle,Native.RIDI_NAME,buffer,ref length)==UInt32.MaxValue)throw new Win32Exception();path=Marshal.PtrToStringUni(buffer);}finally{Marshal.FreeHGlobal(buffer);}
                if(path.IndexOf("VID&0105AC_PID&024F_REV&0003_",StringComparison.OrdinalIgnoreCase)<0||path.IndexOf("&COL02#",StringComparison.OrdinalIgnoreCase)<0)continue;
                string instance=InstanceOf(path),address=AddressOf(instance);if(address==null)continue;
                uint node;if(Native.CM_Locate_DevNodeW(out node,instance,0)!=0)throw new InvalidDataException("鼠标设备已变化，请刷新。");uint status,problem;
                found.Add(new MouseCandidate {Address=address,Container=Native.Container(node),Instance=instance,Class=Property(node,9).Trim('{','}'),Service=Property(node,5),Healthy=CM_Get_DevNode_Status(out status,out problem,node,0)==0&&problem==0&&(status&8)!=0});
            }
            Guid service=VoiceGatt.Service;uint chars;uint code=VoiceNative.CM_Get_Device_Interface_List_SizeW(out chars,ref service,null,0);if(code!=0||chars>1024*1024)throw new InvalidDataException("语音服务目录读取失败。");var paths=new char[Math.Max(2,(int)chars)];if(VoiceNative.CM_Get_Device_Interface_ListW(ref service,null,paths,(uint)paths.Length,0)!=0)throw new InvalidDataException("语音服务目录正在变化。");
            foreach(var c in found){c.MouseMatches=found.Count(x=>x.Address==c.Address&&String.Equals(x.Container,c.Container,StringComparison.OrdinalIgnoreCase));
                var matches=new List<string>();foreach(string path in new string(paths).Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries)){
                    if(path.IndexOf("_"+c.Address+"#",StringComparison.OrdinalIgnoreCase)<0)continue;uint node;if(Native.CM_Locate_DevNodeW(out node,InstanceOf(path),0)!=0)continue;
                    if(String.Equals(Native.Container(node),c.Container,StringComparison.OrdinalIgnoreCase))matches.Add(path);
                }c.VoiceMatches=matches.Count;c.VoicePath=matches.Count==1?matches[0]:"";c.Reason=Check(c);
            }return found;
        }
    }
}
