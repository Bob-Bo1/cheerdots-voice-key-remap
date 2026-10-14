using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CheerCustom;

namespace CheerVoiceLite {
    sealed class LiteWindow:Form {
        internal static readonly string[] Kinds={"打开 Codex","收回 Codex","打开微信","快捷键","打开应用程序","未设置"};
        readonly ActionEditor single=new ActionEditor("单击"),dual=new ActionEditor("双击");
        readonly VoiceToggle enable=new VoiceToggle(),startupToggle=new VoiceToggle();readonly Label title=new Label(),status=new Label(),storage=new Label(),result=new Label();
        readonly TextBox log=new TextBox();readonly Panel dot=new Panel();readonly TableLayoutPanel body=new TableLayoutPanel();
        readonly NotifyIcon tray=new NotifyIcon();readonly ToolTip tips=new ToolTip();
        readonly System.Windows.Forms.Timer receiverTimer=new System.Windows.Forms.Timer {Interval=100},saveTimer=new System.Windows.Forms.Timer {Interval=400};
        readonly List<LiteRunRecord> runRecords=new List<LiteRunRecord>();
        readonly string file;LiteSettings saved=new LiteSettings();LiveLogSession session;Form logWindow;
        StartupController startup;MouseCandidate selectedTarget;bool silent,autoResume;DateTime startupChecked=DateTime.MinValue;
        bool loading=true,dirty,exiting,conditionsConfirmed,storageBlocked,loadFailed;int logLines;
        string availability="正在检查连接…",hint="";DateTime availabilityChecked=DateTime.MinValue,hintUntil=DateTime.MinValue;Color hintColor;
        static readonly Color Muted=Color.FromArgb(110,110,108),Green=Color.FromArgb(43,145,65),Amber=Color.FromArgb(158,105,30),ErrorColor=Color.FromArgb(174,57,46);
        internal const string ConditionsText="官方软件请选“语音转文字”。";

        internal LiteWindow(string configPath=null){
            SuspendLayout();file=configPath??Path.Combine(Data.Dir,"voice-lite.json");Text=AppBrand.Name;Icon=AppBrand.LoadIcon();Font=new Font("Microsoft YaHei UI",10.5f);BackColor=UiPaint.Canvas;ForeColor=UiPaint.Ink;DoubleBuffered=true;
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(540,292);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
            body.Dock=DockStyle.Fill;body.Padding=new Padding(28,24,28,20);body.ColumnCount=1;body.RowCount=6;body.Margin=Padding.Empty;
            foreach(int height in new[]{80,44,8,44,20})body.RowStyles.Add(new RowStyle(SizeType.Absolute,height));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(body);
            var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};header.RowStyles.Add(new RowStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));body.Controls.Add(header,0,0);
            var heading=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};heading.RowStyles.Add(new RowStyle(SizeType.Absolute,40));heading.RowStyles.Add(new RowStyle(SizeType.Percent,100));header.Controls.Add(heading,0,0);
            title.Text=AppBrand.Name;title.Font=new Font(Font.FontFamily,20,FontStyle.Bold);title.Dock=DockStyle.Fill;title.TextAlign=ContentAlignment.MiddleLeft;title.Margin=Padding.Empty;heading.Controls.Add(title,0,0);
            var state=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};state.RowStyles.Add(new RowStyle(SizeType.Percent,100));state.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,20));state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));heading.Controls.Add(state,0,1);
            dot.Size=new Size(10,10);dot.Anchor=AnchorStyles.Left;dot.Margin=Padding.Empty;dot.ForeColor=Muted;dot.Paint+=(o,e)=>{e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(dot.ForeColor))e.Graphics.FillEllipse(brush,0,0,dot.Width-1,dot.Height-1);};state.Controls.Add(dot,0,0);
            status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.AutoEllipsis=true;status.Margin=Padding.Empty;status.ForeColor=Muted;state.Controls.Add(status,1,0);
            var switches=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty,Padding=new Padding(0,0,0,8)};switches.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));switches.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));switches.RowStyles.Add(new RowStyle(SizeType.Percent,50));switches.RowStyles.Add(new RowStyle(SizeType.Percent,50));header.Controls.Add(switches,1,0);
            enable.Margin=Padding.Empty;enable.Anchor=AnchorStyles.Right;enable.Click+=(o,e)=>{if(session!=null&&session.Enabled||autoResume)Disable();else Enable();};switches.Controls.Add(new Label {Text="启用",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Muted,Margin=new Padding(0,0,10,0)},0,0);switches.Controls.Add(enable,1,0);
            startupToggle.Margin=Padding.Empty;startupToggle.Anchor=AnchorStyles.Right;startupToggle.AccessibleName="开机自启";startupToggle.Click+=(o,e)=>ChangeStartup();switches.Controls.Add(new Label {Text="开机自启",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Muted,Margin=new Padding(0,0,10,0)},0,1);switches.Controls.Add(startupToggle,1,1);tips.SetToolTip(startupToggle,"登录 Windows 后在后台启动，并启用已保存的配置");
            single.Dock=DockStyle.Fill;dual.Dock=DockStyle.Fill;body.Controls.Add(single,0,1);body.Controls.Add(dual,0,3);
            var footer=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty};body.Controls.Add(footer,0,5);footer.Controls.Add(new Panel {Dock=DockStyle.Top,Height=1,BackColor=Color.FromArgb(229,229,226)});
            storage.Text="自动保存";storage.ForeColor=Muted;storage.Dock=DockStyle.Fill;storage.Padding=Padding.Empty;storage.TextAlign=ContentAlignment.MiddleLeft;storage.AutoEllipsis=true;footer.Controls.Add(storage);storage.BringToFront();
            foreach(var editor in new[]{single,dual}){var actionEditor=editor;editor.Edited+=Edited;editor.Report+=Note;editor.Test.Click+=(o,e)=>TryAction(actionEditor);tips.SetToolTip(editor.Test,"立即测试这一行的动作");}
            saveTimer.Tick+=(o,e)=>Persist();
            result.Dock=DockStyle.Top;result.Height=60;result.Padding=new Padding(12);result.ForeColor=Muted;log.Dock=DockStyle.Fill;log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Color.White;
            var menu=new ContextMenuStrip();menu.Items.Add("打开设置",null,(o,e)=>Restore());menu.Items.Add("停用",null,(o,e)=>Disable());menu.Items.Add(new ToolStripSeparator());menu.Items.Add("运行记录",null,(o,e)=>ShowLogs());menu.Items.Add("使用说明",null,(o,e)=>MessageBox.Show(this,ConditionsText+"\r\n\r\n单击与双击分别设置，修改自动保存。桌面与 AIR 均接收语音按键；长按由官方处理。","使用说明",MessageBoxButtons.OK,MessageBoxIcon.Information));
            var pin=new ToolStripMenuItem("窗口置顶"){CheckOnClick=true};pin.CheckedChanged+=(o,e)=>TopMost=pin.Checked;menu.Items.Add(pin);menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出",null,(o,e)=>{exiting=true;Close();});tray.Icon=Icon;tray.Text=AppBrand.Name+" · 已停用";tray.ContextMenuStrip=menu;tray.DoubleClick+=(o,e)=>Restore();tray.Visible=true;
            FormClosing+=OnWindowClosing;
            try{startup=new StartupController(Path.Combine(Path.GetDirectoryName(file),"startup.json"),Application.ExecutablePath);startupToggle.Checked=startup.Enabled;}catch(Exception ex){startupToggle.Enabled=false;Note("自启设置未读取："+ex.Message);}
            LoadConfig();loading=false;RefreshStatus();receiverTimer.Tick+=(o,e)=>{if(autoResume)TryAutoEnable();if(session!=null&&session.Enabled)try{session.Poll(InputSettings);}catch(Exception ex){session.Stop();Note("接收已停用："+ex.Message);}else if(DateTime.Now-availabilityChecked>=TimeSpan.FromSeconds(2)&&!autoResume)RefreshAvailability();RefreshStatus();};receiverTimer.Start();
            AutoScaleDimensions=new SizeF(96,96);ResumeLayout(false);PerformAutoScale();UpdateFields();PerformLayout();
        }
        void OnWindowClosing(object sender,FormClosingEventArgs e){
            if(dirty&&!Persist()){e.Cancel=true;Show();Problem("修改尚未保存，请检查提示后再关闭。");return;}
            if(!exiting){e.Cancel=true;Hide();return;}
            receiverTimer.Stop();receiverTimer.Dispose();saveTimer.Stop();saveTimer.Dispose();if(session!=null)session.Dispose();if(logWindow!=null)logWindow.Close();else{log.Dispose();result.Dispose();}tray.Dispose();tips.Dispose();Icon.Dispose();
        }
        void Edited(bool immediate){if(loading)return;dirty=true;UpdateFields();saveTimer.Stop();storage.Text="正在保存…";storage.ForeColor=Muted;if(immediate)Persist();else saveTimer.Start();}
        void UpdateFields(){float scale=body.RowStyles[0].Height/80f;body.RowStyles[1].Height=(single.Expanded?88:44)*scale;body.RowStyles[3].Height=(dual.Expanded?88:44)*scale;int height=(int)Math.Round((292+(single.Expanded?44:0)+(dual.Expanded?44:0))*scale);if(ClientSize.Height!=height)ClientSize=new Size(ClientSize.Width,height);}
        LiteSettings Current(){return new LiteSettings {Single=single.Current(),Double=dual.Current()};}
        LiteSettings InputSettings(){if(storageBlocked||dirty&&!Persist())throw new IOException("修改未保存，接收已暂停。");return saved;}
        void LoadConfig(){try{saved=LiteSettings.Load(file);single.LoadAction(saved.Single);dual.LoadAction(saved.Double);ShowStorage();}catch(Exception ex){loadFailed=true;storageBlocked=true;saved=new LiteSettings {Single=new LiteConfig {Action="未设置"}};single.LoadAction(saved.Single);dual.LoadAction(saved.Double);storage.Text="配置未读取，原文件已保留";Note("配置未读取，原文件已保留："+ex.Message);}}
        bool Persist(){saveTimer.Stop();if(!dirty)return !storageBlocked;try{if(loadFailed)throw new InvalidDataException("原配置未能读取，请先修复原配置；未覆盖原文件。");var next=Current();next.Save(file);saved=next;dirty=false;if(storageBlocked)hintUntil=DateTime.MinValue;storageBlocked=false;ShowStorage();return true;}catch(Exception ex){storageBlocked=true;storage.Text="保存失败，请从托盘查看运行记录";storage.ForeColor=ErrorColor;if(session!=null)session.Stop();Note("保存失败："+ex.Message);return false;}}
        void ShowStorage(){storage.Text="已自动保存";storage.ForeColor=Muted;foreach(var pair in new[]{Tuple.Create("单击",saved.Single),Tuple.Create("双击",saved.Double)})try{LiteActions.Validate(pair.Item2);}catch(Exception ex){storage.Text=pair.Item1+"待补全："+ex.Message;storage.ForeColor=Amber;break;}SetTip(storage,storage.Text);}
        void TryAction(ActionEditor editor){try{if(dirty&&!Persist())return;Note(LiteActions.Run(editor.Current()));}catch(Exception ex){Problem("功能执行失败："+ex.Message);}}
        void Problem(string text){Note(text);if(!silent)MessageBox.Show(this,text,AppBrand.Name,MessageBoxButtons.OK,MessageBoxIcon.Information);}
        void Restore(){silent=false;ShowInTaskbar=true;Show();WindowState=FormWindowState.Normal;Activate();}
        protected override void SetVisibleCore(bool value){if(silent&&value){if(!IsHandleCreated)CreateHandle();base.SetVisibleCore(false);return;}base.SetVisibleCore(value);}
        internal bool StartSilent(){if(startup==null||!startup.Preferences.Enabled)return false;silent=true;ShowInTaskbar=false;autoResume=true;conditionsConfirmed=true;return true;}
        void ChangeStartup(){try{if(startup==null)throw new InvalidOperationException("自启设置无法读取。");var target=selectedTarget;if(target==null){var targets=TargetDiscovery.Scan().Where(x=>TargetDiscovery.Check(x)=="").ToArray();if(targets.Length==1)target=targets[0];}startup.SetEnabled(!startupToggle.Checked,target);startupToggle.Checked=startup.Enabled;Note(startupToggle.Checked?"开机自启已开启，下次登录后台启用当前配置。":"开机自启已关闭。");}catch(Exception ex){Problem("自启设置失败："+ex.Message);}}
        void TryAutoEnable(){
            if(DateTime.Now-startupChecked<TimeSpan.FromSeconds(3))return;startupChecked=DateTime.Now;
            try{if(storageBlocked)throw new InvalidOperationException("配置需要检查，请打开设置");LiteActions.Validate(saved.Single);LiteActions.Validate(saved.Double);var target=startup.Pick(TargetDiscovery.Scan().ToArray());if(session==null)session=new LiveLogSession(Report,c=>Report(LiteActions.Run(c)));session.Start(target);selectedTarget=target;autoResume=false;startup.Remember(target);Note("已自动启用保存的单双击配置。");}
            catch(Exception ex){availability="等待鼠标或官方软件";SetTip(startupToggle,ex.Message);}
        }
        internal void PinForTest(){TopMost=true;foreach(ToolStripItem item in tray.ContextMenuStrip.Items){var choice=item as ToolStripMenuItem;if(choice!=null&&choice.Text=="窗口置顶")choice.Checked=true;}}
        void ShowLogs(){if(logWindow==null){logWindow=new Form {Text="运行记录",ClientSize=new Size(700,420),Font=Font,StartPosition=FormStartPosition.CenterParent,BackColor=UiPaint.Canvas};logWindow.Controls.Add(log);logWindow.Controls.Add(result);logWindow.FormClosing+=(o,e)=>{if(!exiting){e.Cancel=true;logWindow.Hide();}};}logWindow.Show(this);logWindow.Activate();}
        void Note(string text){
            result.Text=text;log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine);if(++logLines>100){log.Clear();logLines=0;}bool failed=text.Contains("失败")||text.Contains("已停用：")||text.Contains("未读取");
            if(!text.StartsWith("收到目标")&&!text.StartsWith("已启用")){hint=text;hintColor=failed?ErrorColor:Muted;hintUntil=DateTime.Now.AddSeconds(failed?8:3);if(failed&&!Visible&&!silent){tray.BalloonTipTitle=AppBrand.Name;tray.BalloonTipText=text;tray.ShowBalloonTip(5000);}}
            runRecords.Add(new LiteRunRecord {Time=DateTimeOffset.Now.ToString("o"),Message=text});while(runRecords.Count>128)runRecords.RemoveAt(0);try{Data.Save(Path.Combine(Data.Dir,"latest-run.json"),runRecords);}catch(Exception ex){result.Text=text+"（运行记录未保存："+ex.Message+"）";}RefreshStatus();
        }
        void Report(string text){try{if(IsDisposed||!IsHandleCreated)return;BeginInvoke((Action)(()=>{if(!IsDisposed)Note(text);}));}catch(InvalidOperationException){}}
        void RefreshAvailability(){availabilityChecked=DateTime.Now;try{int count=TargetDiscovery.Scan().Count(x=>TargetDiscovery.Check(x)=="");if(count==0){availability="鼠标未连接";return;}if(count>1){availability="启用时请选择鼠标";return;}var processes=Process.GetProcessesByName("Cheerdots");try{availability=processes.Length==0?"请打开官方软件":processes.Length>1?"发现多个官方程序":"鼠标已连接";}finally{foreach(var p in processes)p.Dispose();}}catch{availability="暂时无法检查连接";}}
        void RefreshStatus(){bool active=session!=null&&session.Enabled,connected=active&&session.Connected;enable.Checked=active||autoResume;string text=active?(connected?"鼠标已连接":"已暂停，等待鼠标或官方软件"):availability;Color color=active?(connected?Green:Amber):availability=="鼠标已连接"?Green:Muted;if(dot.ForeColor!=color){dot.ForeColor=color;dot.Invalidate();}string shown=DateTime.Now<hintUntil?hint:text;Color shownColor=DateTime.Now<hintUntil?hintColor:Muted;if(status.Text!=shown)status.Text=shown;if(status.ForeColor!=shownColor)status.ForeColor=shownColor;SetTip(status,shown);string trayText=AppBrand.Name+" · "+(active?(connected?"已启用":"已暂停"):autoResume?"等待就绪":"已停用");if(tray.Text!=trayText)tray.Text=trayText;SetTip(enable,active||autoResume?"停用语音键":"启用语音键");}
        void SetTip(Control control,string text){if(tips.GetToolTip(control)!=text)tips.SetToolTip(control,text);}
        void Disable(){autoResume=false;if(session!=null)session.Stop();conditionsConfirmed=false;availabilityChecked=DateTime.MinValue;Note("已停用。");RefreshStatus();}
        void Enable(){
            autoResume=false;
            if(session!=null&&session.Enabled)return;if(dirty&&!Persist())return;if(storageBlocked){Problem("请先修正配置保存问题。");return;}
            try{LiteActions.Validate(saved.Single);LiteActions.Validate(saved.Double);
                if(!conditionsConfirmed){if(MessageBox.Show(this,ConditionsText,"启用语音键",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;conditionsConfirmed=true;}
                var candidates=TargetDiscovery.Scan().Where(x=>TargetDiscovery.Check(x)=="").ToList();if(candidates.Count==0)throw new InvalidOperationException("没有找到已连接的 Cheerdots 鼠标，请检查电源和蓝牙连接。");MouseCandidate target;
                if(candidates.Count==1)target=candidates[0];else using(var dialog=new Form {Text="选择要控制的Cheerdots",ClientSize=new Size(680,150),StartPosition=FormStartPosition.CenterParent,Font=Font}){var choices=new ComboBox {Left=20,Top=20,Width=640,DropDownStyle=ComboBoxStyle.DropDownList};choices.Items.AddRange(candidates.Cast<object>().ToArray());var choose=new Button {Text="使用选中鼠标",Left=20,Top=75,Width=150,DialogResult=DialogResult.OK};dialog.Controls.Add(choices);dialog.Controls.Add(choose);if(dialog.ShowDialog(this)!=DialogResult.OK||choices.SelectedItem==null)return;target=(MouseCandidate)choices.SelectedItem;}
                if(session==null)session=new LiveLogSession(Report,c=>Report(LiteActions.Run(c)));session.Start(target);selectedTarget=target;if(startup!=null)startup.Remember(target);hintUntil=DateTime.MinValue;RefreshStatus();Note("已启用："+target.Address+"；使用当前单双击配置。");
            }catch(Exception ex){conditionsConfirmed=false;Problem("启用失败："+ex.Message);RefreshStatus();}
        }
        internal void Preview(string path){Show();Application.DoEvents();RefreshAvailability();RefreshStatus();CapturePreview(path);exiting=true;Close();}
        void CapturePreview(string path){using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path);}}
        void CheckLayout(){CheckChildren(this);}
        static void CheckChildren(Control parent){foreach(Control child in parent.Controls)if(child.Visible){if(child.Left<0||child.Top<0||child.Right>parent.ClientSize.Width||child.Bottom>parent.ClientSize.Height)throw new Exception("控件被裁切："+child.GetType().Name+" "+child.Text+" "+child.Bounds+" / "+parent.ClientSize);CheckChildren(child);}}
        void PumpSave(){var end=DateTime.UtcNow.AddMilliseconds(600);while(DateTime.UtcNow<end){Application.DoEvents();System.Threading.Thread.Sleep(10);}}
        internal void Exercise(){
            Show();Application.DoEvents();single.Kind.SelectedItem="打开 Codex";dual.Kind.SelectedItem="未设置";Application.DoEvents();int compact=ClientSize.Height;CheckLayout();
            single.Kind.SelectedItem="快捷键";single.Value.Text="Ctrl+Shift+A";dual.Kind.SelectedItem="收回 Codex";PumpSave();var config=LiteSettings.Load(file);if(config.Single.Value!="Ctrl+Shift+A"||config.Double.Action!="收回 Codex")throw new Exception("单双击自动保存失败");CheckLayout();
            dual.Kind.SelectedItem="快捷键";dual.Value.Text="Ctrl+Alt+B";PumpSave();CheckLayout();if(ClientSize.Height<=compact)throw new Exception("输入字段未展开");
            single.Value.Text="Ctrl+";PumpSave();if(LiteSettings.Load(file).Single.Value!="Ctrl+"||!storage.Text.Contains("待补全"))throw new Exception("未完成输入应保留草稿并提示");
            single.Kind.SelectedItem="打开应用程序";single.Value.Text=Application.ExecutablePath;PumpSave();CheckLayout();
            single.Kind.SelectedItem="打开 Codex";dual.Kind.SelectedItem="未设置";PumpSave();CheckLayout();if(ClientSize.Height!=compact)throw new Exception("字段收回后窗口高度漂移");
            CapturePreview(Path.Combine(Data.Dir,"checks","compact-100.png"));foreach(float factor in new[]{1.25f,1.2f}){Font=new Font(Font.FontFamily,Font.Size*factor);title.Font=new Font(title.Font.FontFamily,title.Font.Size*factor,title.Font.Style);Scale(new SizeF(factor,factor));UpdateFields();Application.DoEvents();CheckLayout();CapturePreview(Path.Combine(Data.Dir,"checks","compact-"+(factor==1.25f?"125":"150")+".png"));}
            for(int i=0;i<220;i++)Note("运行记录容量检查");if(log.Lines.Length>102||runRecords.Count>128)throw new Exception("日志超出容量");ShowLogs();logWindow.Close();if(logWindow.Visible)throw new Exception("日志未隐藏");Disable();Close();if(Visible||!tray.Visible)throw new Exception("关闭未进托盘");Restore();if(!Visible)throw new Exception("托盘恢复失败");exiting=true;Close();
        }
    }
}
