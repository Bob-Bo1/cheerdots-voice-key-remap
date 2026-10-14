using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CheerCustom {
    static class Native {
        public const int WM_INPUT=0xFF,WM_INPUT_DEVICE_CHANGE=0xFE,WM_HOTKEY=0x312;
        public const uint RID_INPUT=0x10000003,RIDI_NAME=0x20000007,RIDI_INFO=0x2000000B,RIDI_PREPARSED=0x20000005;
        [StructLayout(LayoutKind.Sequential)] public struct DeviceList { public IntPtr Handle; public uint Type; }
        [StructLayout(LayoutKind.Sequential)] public struct Registration { public ushort Page,Usage; public uint Flags; public IntPtr Window; }
        [StructLayout(LayoutKind.Sequential)] public struct Header {public uint Type,Size;public IntPtr Device,WParam;}
        [StructLayout(LayoutKind.Sequential)] public struct PropertyKey {public Guid Format;public uint Id; public PropertyKey(Guid g,uint id){Format=g;Id=id;}}
        [StructLayout(LayoutKind.Sequential)] public struct KeyInput {public ushort Vk,Scan; public uint Flags,Time;public UIntPtr Extra;}
        [StructLayout(LayoutKind.Explicit,Size=40)] public struct Input { [FieldOffset(0)]public uint Type; [FieldOffset(8)]public KeyInput Key;}
        [DllImport("user32.dll",SetLastError=true)] public static extern uint GetRawInputDeviceList([Out]DeviceList[] a,ref uint count,uint size);
        [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)] public static extern uint DeviceInfo(IntPtr dev,uint cmd,IntPtr data,ref uint size);
        [DllImport("user32.dll",SetLastError=true)]public static extern uint SendInput(uint count,Input[] inputs,int size);
        [DllImport("user32.dll")]public static extern short GetAsyncKeyState(int vk);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]public static extern uint CM_Locate_DevNodeW(out uint node,string id,uint flags);
        [DllImport("cfgmgr32.dll")]public static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]public static extern uint CM_Get_DevNode_Registry_PropertyW(uint node,uint prop,out uint type,byte[] data,ref uint length,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]public static extern uint CM_Get_DevNode_PropertyW(uint node,ref PropertyKey key,out uint type,byte[] data,ref uint length,uint flags);
        public static string Name(uint node){uint typ,len=2048;var b=new byte[len];if(CM_Get_DevNode_Registry_PropertyW(node,13,out typ,b,ref len,0)!=0){len=2048;if(CM_Get_DevNode_Registry_PropertyW(node,1,out typ,b,ref len,0)!=0)return "";}return Encoding.Unicode.GetString(b,0,(int)len).TrimEnd('\0');}
        public static string Container(uint node){var k=new PropertyKey(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"),2);uint typ,len=16;var b=new byte[16];if(CM_Get_DevNode_PropertyW(node,ref k,out typ,b,ref len,0)==0 && len==16)return new Guid(b).ToString();return "";}
    }
}
