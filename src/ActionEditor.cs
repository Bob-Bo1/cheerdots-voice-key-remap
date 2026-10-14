using System;
using System.Drawing;
using System.Windows.Forms;
using CheerCustom;

namespace CheerVoiceLite {
    sealed class ActionEditor:UserControl {
        internal readonly RoundedChoice Kind=new RoundedChoice();
        internal readonly TextBox Value=new TextBox();
        internal readonly Button Browse=new RoundedActionButton(),Record=new RoundedActionButton(),Test=new RoundedActionButton {Primary=false};
        readonly TableLayoutPanel body=new TableLayoutPanel(),details=new TableLayoutPanel();
        readonly Label label=new Label();readonly VoiceShortcutSequence sequence=new VoiceShortcutSequence();
        bool loading,recording;internal event Action<bool> Edited;internal event Action<string> Report;
        internal bool Expanded {get{return Kind.Text=="快捷键"||Kind.Text=="打开应用程序";}}
        internal ActionEditor(string gesture){
            SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;BackColor=UiPaint.Canvas;Margin=Padding.Empty;TabStop=false;
            body.Dock=DockStyle.Fill;body.ColumnCount=1;body.RowCount=2;body.Margin=Padding.Empty;body.RowStyles.Add(new RowStyle(SizeType.Absolute,44));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(body);
            var row=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));body.Controls.Add(row,0,0);
            row.Controls.Add(new Label {Text=gesture,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty},0,0);
            Kind.Items.AddRange(LiteWindow.Kinds);Kind.Dock=DockStyle.Fill;Kind.Margin=new Padding(0,4,8,4);Kind.AccessibleName=gesture+"动作";row.Controls.Add(Kind,1,0);
            Test.Text="测试";Test.Dock=DockStyle.Fill;Test.Margin=new Padding(0,4,0,4);Test.AccessibleName="测试"+gesture;row.Controls.Add(Test,2,0);
            details.Dock=DockStyle.Fill;details.Margin=Padding.Empty;details.ColumnCount=3;details.RowCount=1;details.RowStyles.Add(new RowStyle(SizeType.Percent,100));details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,64));details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,94));body.Controls.Add(details,0,1);
            label.Dock=DockStyle.Fill;label.TextAlign=ContentAlignment.MiddleLeft;label.ForeColor=Color.FromArgb(110,110,108);label.Margin=Padding.Empty;details.Controls.Add(label,0,0);
            Value.AccessibleName=gesture+"动作内容";Value.MaxLength=4096;details.Controls.Add(new RoundedTextField(Value){Dock=DockStyle.Fill,Margin=new Padding(0,4,10,4)},1,0);
            var buttons=new Panel {Dock=DockStyle.Fill,Margin=new Padding(0,4,0,4)};details.Controls.Add(buttons,2,0);
            Browse.Text="选择文件";Browse.Dock=DockStyle.Fill;buttons.Controls.Add(Browse);Record.Text="录入按键";Record.Dock=DockStyle.Fill;buttons.Controls.Add(Record);
            Kind.SelectedIndexChanged+=(o,e)=>{if(loading)return;loading=true;Value.Text="";loading=false;recording=false;sequence.Clear();RefreshFields();Changed(true);};
            Value.TextChanged+=(o,e)=>{if(!loading)Changed(false);};
            Browse.Click+=(o,e)=>{using(var dialog=new OpenFileDialog {Filter="程序与快捷方式|*.exe;*.lnk",CheckFileExists=true})if(dialog.ShowDialog(this)==DialogResult.OK){Value.Text=dialog.FileName;Changed(true);}};
            Record.Click+=(o,e)=>{recording=true;sequence.Clear();Value.Focus();if(Report!=null)Report("请按下组合键，Esc 取消。");};
            Value.Leave+=(o,e)=>{recording=false;sequence.Clear();};
            ResumeLayout(false);
        }
        void Changed(bool immediate){if(Edited!=null)Edited(immediate);}
        internal void LoadAction(LiteConfig config){loading=true;Kind.SelectedItem=config.Action;Value.Text=config.Value;loading=false;RefreshFields();}
        internal LiteConfig Current(){return new LiteConfig {Action=Kind.Text,Value=Expanded?Value.Text:""};}
        void RefreshFields(){bool custom=Kind.Text=="打开应用程序",shortcut=Kind.Text=="快捷键";details.Visible=custom||shortcut;Value.Enabled=custom||shortcut;label.Text=custom?"地址":"组合键";Browse.Visible=custom;Record.Visible=shortcut;}
        protected override bool ProcessCmdKey(ref Message message,Keys keys){
            if(recording&&Value.Focused){keys=VoiceClickEditor.NormalizeKey(ref message,keys);if((keys&Keys.KeyCode)==Keys.Escape){recording=false;sequence.Clear();return true;}string text;if(sequence.Capture(keys,out text)){if(text!=null){Value.Text=text;recording=false;Changed(true);}return true;}}
            return base.ProcessCmdKey(ref message,keys);
        }
    }
}
