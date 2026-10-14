using System;using System.Linq;using System.Runtime.InteropServices;
namespace CheerCustom {
    sealed class VoiceControlFrame {
        public byte Header,Code;
        public static bool TryRead(byte[] p,string target,out VoiceControlFrame frame){
            frame=null;
            if(p==null||p.Length!=16||p[0]!=10||(p[1]!=12&&p[1]!=8)||p[2]!=0||p[3]!=80||p[4]!=52||p[6]!=1||p[7]!=5||p[8]!=0||p[9]!=0)return false;
            if(p[5]!=0x21&&p[5]!=0x22&&p[5]!=0x41&&p[5]!=0x42)return false;
            if(target==null||target.Length!=12)return false;
            try{if(!p.Skip(10).SequenceEqual(VoiceGatt.AddressBytes(target)))return false;}catch{return false;}
            frame=new VoiceControlFrame {Header=p[1],Code=p[5]};return true;
        }
    }
    static class VoiceGatt {
        public static readonly Guid Service=new Guid("00010203-0405-0607-0809-0a0b0c0d1910");
        internal static byte[] AddressBytes(string address){return Enumerable.Range(0,6).Select(i=>Convert.ToByte(address.Substring(i*2,2),16)).ToArray();}
    }
    static class VoiceNative {
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]public static extern uint CM_Get_Device_Interface_List_SizeW(out uint length,ref Guid guid,string id,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]public static extern uint CM_Get_Device_Interface_ListW(ref Guid guid,string id,[Out]char[] list,uint length,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]public static extern uint CM_Get_Device_Interface_PropertyW(string path,ref Native.PropertyKey key,out uint type,byte[] data,ref uint length,uint flags);
    }
}