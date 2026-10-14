using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using CheerCustom;

namespace CheerVoiceLite {
    public sealed class OfficialLogEvent {
        public string ObservedAt,LoggedAt,FileName,FileId,Raw,Address;
        public long EndOffset;
        public int Header,Code;
        public double LogAgeUpperBoundMs;
    }
    // Only the vendor's control-frame log lines are retained. Account/audio text is discarded.
    sealed class OfficialLogReader:IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct FileInfo {
            public uint Attributes,CreatedLow,CreatedHigh,AccessLow,AccessHigh,WriteLow,WriteHigh,Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
        }
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInfo info);
        sealed class Cursor {public long Offset;public readonly List<byte> Pending=new List<byte>();public bool DiscardPartial;}
        static readonly Regex Pattern=new Regex(@"^(?<time>\d{4},\d{2},\d{2} \d{2}:\d{2}:\d{2}).*\{[^}]*DeviceBusinessProcessor::onBusiness\([^}]*\}.*?(?<frame>0a[ -]0[89c][ -]00[ -]50[ -]34(?:[ -][0-9a-f]{2}){11})(?![ -][0-9a-f]{2})",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        readonly string directory,address;readonly DateTime started;
        readonly Dictionary<string,Cursor> cursors=new Dictionary<string,Cursor>();bool disposed;
        public OfficialLogReader(string folder,string target){
            directory=folder;address=target;started=DateTime.Now;
            foreach(var file in Files())using(var stream=Open(file)){string id=Identity(stream);if(cursors.ContainsKey(id))continue;long size=stream.Length;bool partial=false;if(size>0){stream.Position=size-1;partial=stream.ReadByte()!=10;}cursors.Add(id,new Cursor {Offset=size,DiscardPartial=partial});}
        }
        string[] Files(){if(!Directory.Exists(directory))throw new DirectoryNotFoundException("官方日志目录不存在。");var files=Directory.GetFiles(directory,"umouse*.log");if(files.Length>16)throw new IOException("官方日志文件超过16份，停止本轮检查。");return files.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();}
        static FileStream Open(string path){return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);}
        static string Identity(FileStream stream){FileInfo info;if(!GetFileInformationByHandle(stream.SafeFileHandle,out info))throw new IOException("无法核对日志文件身份。",Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));return info.Volume.ToString("X8")+":"+info.IndexHigh.ToString("X8")+info.IndexLow.ToString("X8");}
        internal static OfficialLogEvent Parse(string line,string target,DateTime earliest){
            var match=Pattern.Match(line);if(!match.Success)return null;DateTime time;
            if(!DateTime.TryParseExact(match.Groups["time"].Value,"yyyy,MM,dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out time)||time<earliest.AddSeconds(-1))return null;
            var bytes=match.Groups["frame"].Value.Split(new[]{' ','-'},StringSplitOptions.RemoveEmptyEntries).Select(x=>Convert.ToByte(x,16)).ToArray();VoiceControlFrame frame;
            // 09 is present in the target's AIR control log. Keep this extension local
            // to the log route; do not broaden the older direct-GATT mapping gates.
            var schema=bytes;if(bytes[1]==9){schema=(byte[])bytes.Clone();schema[1]=8;}
            if(!VoiceControlFrame.TryRead(schema,target,out frame)||(frame.Code!=0x41&&frame.Code!=0x42))return null;
            var now=DateTimeOffset.Now;return new OfficialLogEvent {Header=bytes[1],Code=frame.Code,ObservedAt=now.ToString("o"),LoggedAt=time.ToString("yyyy-MM-dd HH:mm:ss"),Raw=BitConverter.ToString(bytes),Address=target,LogAgeUpperBoundMs=(now.LocalDateTime-time).TotalMilliseconds};
        }
        public List<OfficialLogEvent> Poll(){
            if(disposed)throw new ObjectDisposedException("OfficialLogReader");var found=new List<OfficialLogEvent>();long scanned=0;
            foreach(var file in Files()){
                FileStream stream;try{stream=Open(file);}catch(FileNotFoundException){continue;}
                using(stream){string id=Identity(stream);Cursor cursor;if(!cursors.TryGetValue(id,out cursor)){if(cursors.Count>=32)throw new IOException("日志文件身份超过32项，停止本轮检查。");cursor=new Cursor();cursors.Add(id,cursor);}
                    long size=stream.Length;if(size<cursor.Offset)throw new IOException("官方日志被截断，停止本轮以避免重放旧消息。");
                    if((scanned+=size-cursor.Offset)>2*1024*1024)throw new IOException("新增日志超过单轮读取上限，停止本轮检查。");
                    stream.Position=cursor.Offset;var buffer=new byte[4096];int count;
                    while(stream.Position<size&&(count=stream.Read(buffer,0,(int)Math.Min(buffer.Length,size-stream.Position)))>0){
                        for(int i=0;i<count;i++){cursor.Offset++;if(buffer[i]==10){if(!cursor.DiscardPartial){string line=Encoding.UTF8.GetString(cursor.Pending.ToArray()).TrimEnd('\r');var entry=Parse(line,address,started);if(entry!=null){entry.FileName=Path.GetFileName(file);entry.FileId=id;entry.EndOffset=cursor.Offset;found.Add(entry);if(found.Count>2048)throw new IOException("单轮目标消息超过2048项。");}}cursor.Pending.Clear();cursor.DiscardPartial=false;}
                            else if(!cursor.DiscardPartial){if(cursor.Pending.Count>=8192){cursor.Pending.Clear();cursor.DiscardPartial=true;}else cursor.Pending.Add(buffer[i]);}}
                    }
                }
            }return found;
        }
        public void Dispose(){disposed=true;cursors.Clear();}
        internal static string[] Checks(string folder,string target){
            Directory.CreateDirectory(folder);string file=Path.Combine(folder,"umouse-test.log");string frame="0a 08 00 50 34 41 01 05 00 00 "+String.Join(" ",Enumerable.Range(0,6).Select(i=>target.Substring(i*2,2)));Func<string,string> line=raw=>DateTime.Now.ToString("yyyy,MM,dd HH:mm:ss")+" [1] {DeviceBusinessProcessor::onBusiness(const QByteArray &,int)} DEBUG - "+raw;
            string sample=line(frame);if(Parse(sample,target,DateTime.Now)==null||Parse(sample,target=="A1B2C3D4E5F6"?"B1B2C3D4E5F6":"A1B2C3D4E5F6",DateTime.Now)!=null||Parse(line(frame.Replace("34 41","34 42")),target,DateTime.Now).Code!=0x42||Parse(sample.Replace("::onBusiness","::handleKeyEvent"),target,DateTime.Now)!=null)throw new Exception("日志来源或帧筛选失败");
            foreach(var header in new[]{"08","09","0c"})foreach(var code in new[]{"41","42"}){var item=Parse(line(frame.Replace("0a 08","0a "+header).Replace("34 41","34 "+code)),target,DateTime.Now);if(item==null||item.Header!=Convert.ToInt32(header,16)||item.Code!=Convert.ToInt32(code,16))throw new Exception("模式或单双击帧识别失败");}
            foreach(var code in new[]{"00","21","22","43"})if(Parse(line(frame.Replace("34 41","34 "+code)),target,DateTime.Now)!=null)throw new Exception("其他消息不应执行动作");
            File.WriteAllText(file,sample+"\n",new UTF8Encoding(false));
            using(var reader=new OfficialLogReader(folder,target)){
                if(reader.Poll().Count!=0)throw new Exception("旧消息被重放");File.AppendAllText(file,sample,new UTF8Encoding(false));if(reader.Poll().Count!=0)throw new Exception("半行被派发");File.AppendAllText(file,"\n",new UTF8Encoding(false));if(reader.Poll().Count!=1||reader.Poll().Count!=0)throw new Exception("新增消息重复或丢失");
                string rotated=Path.Combine(folder,"umouse-rotated.log");File.Move(file,rotated);if(reader.Poll().Count!=0)throw new Exception("日志改名导致重放");File.WriteAllText(file,sample+"\n",new UTF8Encoding(false));if(reader.Poll().Count!=1)throw new Exception("新日志未继续读取");
                File.WriteAllText(file,"");bool rejected=false;try{reader.Poll();}catch(IOException){rejected=true;}if(!rejected)throw new Exception("日志截断未停止");
            }
            return new[]{"目标地址/单击/官方控制日志来源筛选通过","从文件末尾开始，半行等待完成，重复轮询不派发","日志改名不重放，新日志继续读取，截断立即停止"};
        }
    }
}
