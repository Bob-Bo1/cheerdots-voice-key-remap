using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using CheerCustom;

namespace CheerVoiceLite {
    sealed class LogActionQueue:IDisposable {
        sealed class Work {public LiteConfig Config;public int Generation;public string Gesture;}
        readonly object sync=new object();readonly Queue<Work> pending=new Queue<Work>();readonly AutoResetEvent wake=new AutoResetEvent(false);readonly Thread worker;
        readonly Action<LiteConfig> execute;readonly Action<string> report;int generation;bool active,disposed;internal bool InFlight {get;private set;}
        internal LogActionQueue(Action<LiteConfig> run,Action<string> result){execute=run;report=result;worker=new Thread(Loop){IsBackground=false,Name="Cheerdots single actions"};worker.SetApartmentState(ApartmentState.STA);worker.Start();}
        internal void Start(){lock(sync){if(disposed)throw new ObjectDisposedException("LogActionQueue");generation++;pending.Clear();active=true;}}
        internal void Stop(){lock(sync){active=false;generation++;pending.Clear();}}
        internal void Enqueue(LiteConfig config,string gesture="单击"){lock(sync){if(!active||disposed)return;if(pending.Count>=16)throw new InvalidOperationException("动作等待队列已满，停止接收以免继续积压。");pending.Enqueue(new Work {Config=new LiteConfig {Version=config.Version,Action=config.Action,Value=config.Value},Generation=generation,Gesture=gesture});wake.Set();}}
        void Loop(){while(true){wake.WaitOne();while(true){Work work;lock(sync){if(disposed)return;if(!active||pending.Count==0)break;work=pending.Dequeue();if(work.Generation!=generation)continue;InFlight=true;}
                    try{if(work.Config.Action=="未设置")report(work.Gesture+"未设置，没有执行动作。");else{LiteActions.Validate(work.Config);execute(work.Config);}}catch(Exception ex){report(work.Gesture+"动作失败："+ex.Message);}finally{lock(sync)InFlight=false;}
                }}}
        public void Dispose(){lock(sync){active=false;disposed=true;generation++;pending.Clear();wake.Set();}if(worker.Join(100))wake.Dispose();/* A committed action may finish before the background worker exits. */}
        internal static string[] Checks(){
            var calls=new List<string>();using(var began=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))using(var firstDone=new ManualResetEvent(false))using(var failed=new ManualResetEvent(false))using(var lastDone=new ManualResetEvent(false)){
                using(var queue=new LogActionQueue(c=>{if(c.Action=="打开 Codex"){began.Set();if(!release.WaitOne(3000))throw new Exception("动作替身等待超时");}lock(calls)calls.Add(c.Action);if(c.Action=="打开 Codex")firstDone.Set();if(c.Action=="快捷键")throw new Exception("替身失败");if(c.Action=="打开微信")lastDone.Set();},s=>{if(s.Contains("替身失败"))failed.Set();})){
                    queue.Start();var config=new LiteConfig();queue.Enqueue(config);config.Action="打开微信";if(!began.WaitOne(3000))throw new Exception("动作线程未开始");queue.Enqueue(new LiteConfig {Action="打开微信"});queue.Stop();release.Set();if(!firstDone.WaitOne(3000))throw new Exception("已开始动作未完成");
                    queue.Start();queue.Enqueue(new LiteConfig {Action="快捷键",Value="Ctrl+A"});queue.Enqueue(new LiteConfig {Action="打开微信"});if(!failed.WaitOne(3000)||!lastDone.WaitOne(3000))throw new Exception("动作失败后未继续");lock(calls)if(!calls.SequenceEqual(new[]{"打开 Codex","快捷键","打开微信"}))throw new Exception("配置快照或停用队列清空失败");
                }
            }return new[]{"执行线程保留配置快照，停用清空等待动作（替身）","动作失败记录原因，下一条动作仍执行（替身）"};
        }
    }
    sealed class LiveLogSession:IDisposable {
        readonly Action<string> report;readonly LogActionQueue actions;OfficialLogReader reader;MouseCandidate target;int officialPid;string officialExe;DateTime checkedAt;
        readonly HashSet<string> seen=new HashSet<string>();readonly Queue<string> recent=new Queue<string>();
        internal bool Enabled {get;private set;}internal bool Connected {get{return reader!=null;}}
        internal string Description {get{return target==null?"未启用":target.Address+(Connected?" · 日志接收中":" · 等待目标鼠标或官方软件");}}
        internal LiveLogSession(Action<string> result,Action<LiteConfig> execute){report=result;actions=new LogActionQueue(execute,result);}
        internal void Start(MouseCandidate choice){Stop();if(TargetDiscovery.Check(choice)!="")throw new InvalidOperationException("目标鼠标身份尚未通过核对。");target=choice;Connect();Enabled=true;actions.Start();report("已启用语音键；识别单击和双击，长按保持官方处理。");}
        void Connect(){
            var choices=TargetDiscovery.Scan().Where(x=>x.Address==target.Address&&String.Equals(x.Container,target.Container,StringComparison.OrdinalIgnoreCase)&&TargetDiscovery.Check(x)=="").ToList();if(choices.Count!=1)throw new InvalidOperationException("原目标鼠标未连接或身份发生变化。");
            var processes=Process.GetProcessesByName("Cheerdots");try{if(processes.Length==0)throw new InvalidOperationException("Cheerdots官方软件未运行。请先打开它并选择语音转文字。");if(processes.Length>1)throw new InvalidOperationException("发现多个Cheerdots官方进程，无法确定日志来源。");officialPid=processes[0].Id;officialExe=processes[0].MainModule.FileName;reader=new OfficialLogReader(Path.Combine(Path.GetDirectoryName(officialExe),"logs"),target.Address);checkedAt=DateTime.Now;seen.Clear();recent.Clear();}finally{foreach(var p in processes)p.Dispose();}
        }
        void Verify(){
            using(var p=Process.GetProcessById(officialPid))if(p.HasExited||!String.Equals(p.MainModule.FileName,officialExe,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("官方进程发生变化。");
            var choices=TargetDiscovery.Scan().Where(x=>x.Address==target.Address&&String.Equals(x.Container,target.Container,StringComparison.OrdinalIgnoreCase)&&TargetDiscovery.Check(x)=="").ToList();if(choices.Count!=1)throw new InvalidOperationException("目标鼠标已断开或身份发生变化。");
        }
        internal void Poll(Func<LiteSettings> current){if(!Enabled)return;var now=DateTime.Now;
            if(reader==null){if(now-checkedAt<TimeSpan.FromSeconds(2))return;checkedAt=now;try{Connect();actions.Start();report("原目标已恢复，继续读取新消息。");}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}catch(DirectoryNotFoundException){}return;}
            if(now-checkedAt>=TimeSpan.FromSeconds(1)){checkedAt=now;try{Verify();}catch(Exception ex){Suspend();report("接收暂停："+ex.Message);return;}}
            try{foreach(var entry in reader.Poll()){
                    var key=entry.FileId+":"+entry.EndOffset;if(!seen.Add(key))continue;recent.Enqueue(key);while(recent.Count>64)seen.Remove(recent.Dequeue());var config=current().ForCode(entry.Code);if(config==null)continue;string gesture=entry.Code==0x41?"单击":"双击";report("收到目标"+gesture+" · "+entry.LoggedAt+" · 帧"+entry.Header.ToString("X2")+" · "+entry.FileName+":"+entry.EndOffset+" · 配置："+config.Action);actions.Enqueue(config,gesture);
                }}catch(Exception ex){Stop();report("接收已停用："+ex.Message);}
        }
        void Suspend(){if(reader!=null)reader.Dispose();reader=null;actions.Stop();seen.Clear();recent.Clear();}
        internal void Stop(){Enabled=false;Suspend();}
        public void Dispose(){Stop();actions.Dispose();}
        internal static string[] Checks(string folder){
            Directory.CreateDirectory(folder);string path=Path.Combine(folder,"umouse-dual.log");File.WriteAllText(path,"");
            var calls=new List<string>();var notes=new List<string>();var config=new LiteSettings {Double=new LiteConfig {Action="收回 Codex"}};
            using(var delivered=new AutoResetEvent(false))using(var session=new LiveLogSession(s=>{lock(notes)notes.Add(s);},c=>{lock(calls)calls.Add(c.Action);delivered.Set();})){
                session.reader=new OfficialLogReader(folder,"A1B2C3D4E5F6");session.Enabled=true;session.actions.Start();
                Action<string,string> append=(header,code)=>File.AppendAllText(path,DateTime.Now.ToString("yyyy,MM,dd HH:mm:ss")+" [1] {DeviceBusinessProcessor::onBusiness(const QByteArray &,int)} DEBUG - Raw hex: \"0a "+header+" 00 50 34 "+code+" 01 05 00 00 a1 b2 c3 d4 e5 f6\"\n");
                foreach(var pair in new[]{new[]{"08","41"},new[]{"09","42"},new[]{"09","41"},new[]{"08","42"}}){append(pair[0],pair[1]);session.checkedAt=DateTime.Now;session.Poll(()=>config);if(!delivered.WaitOne(2000))throw new Exception("单双击动作替身未收到");session.checkedAt=DateTime.Now;session.Poll(()=>config);}
                lock(calls)if(!calls.SequenceEqual(new[]{"打开 Codex","收回 Codex","打开 Codex","收回 Codex"}))throw new Exception("双击追加了单击或模式消息丢失");
                append("09","21");append("09","22");append("09","00");session.checkedAt=DateTime.Now;session.Poll(()=>config);lock(calls)if(calls.Count!=4)throw new Exception("长按或模式切换消息不应执行动作");
                config.Single=new LiteConfig {Action="打开微信"};append("09","41");session.checkedAt=DateTime.Now;session.Poll(()=>config);if(!delivered.WaitOne(2000))throw new Exception("运行中新配置未执行");lock(calls)if(calls.Last()!="打开微信")throw new Exception("仍执行旧配置");
                session.Stop();int before;lock(calls)before=calls.Count;append("09","42");session.Poll(()=>config);lock(calls)if(calls.Count!=before||session.recent.Count!=0)throw new Exception("停用未释放或仍派发");
                lock(notes)if(notes.Any(s=>s.Contains("失败")||s.Contains("已停用：")))throw new Exception("日志路径检查发生错误");
            }
            return new[]{"08/09单双击经过日志读取与生产分发，仅各执行对应一次（替身）","重复轮询不重放，双击不追加单击，长按/切换消息忽略","运行中新配置从下条消息生效；停用后拒绝派发并清理去重"};
        }
    }
}
