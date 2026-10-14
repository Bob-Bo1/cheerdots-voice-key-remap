using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CheerVoiceLite {
    static class UiPaint {
        internal static readonly Color Canvas=Color.FromArgb(250,250,249),Ink=Color.FromArgb(43,43,41),Border=Color.FromArgb(216,216,212);
        internal static void Box(Graphics graphics,RectangleF bounds,float radius,Color fill,Color border){
            graphics.SmoothingMode=SmoothingMode.AntiAlias;
            float diameter=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
            using(var path=new GraphicsPath()){
                path.AddArc(bounds.X,bounds.Y,diameter,diameter,180,90);path.AddArc(bounds.Right-diameter,bounds.Y,diameter,diameter,270,90);
                path.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);path.AddArc(bounds.X,bounds.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();
                using(var brush=new SolidBrush(fill))graphics.FillPath(brush,path);
                if(border!=Color.Empty)using(var pen=new Pen(border,1))graphics.DrawPath(pen,path);
            }
        }
    }

    // A plain control has no native checkbox glyph or caption to paint beneath the switch.
    sealed class VoiceToggle:Control {
        bool isChecked,keyDown;
        internal bool Checked {get{return isChecked;}set{if(isChecked==value)return;isChecked=value;Invalidate();AccessibilityNotifyClients(AccessibleEvents.StateChange,-1);}}
        internal VoiceToggle(){
            Size=new Size(48,28);Cursor=Cursors.Default;TabStop=true;BackColor=UiPaint.Canvas;
            AccessibleName="启用语音键";AccessibleRole=AccessibleRole.CheckButton;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
        }
        protected override void OnPaint(PaintEventArgs e){
            using(var background=new SolidBrush(Parent==null?BackColor:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);
            float unit=Height/28f,focusInset=2*unit;
            var track=new RectangleF(focusInset,focusInset,Width-2*focusInset,Height-2*focusInset);
            UiPaint.Box(e.Graphics,track,track.Height/2,Checked?UiPaint.Ink:Color.FromArgb(190,190,186),Color.Empty);
            float diameter=track.Height-4*unit,x=Checked?track.Right-diameter-2*unit:track.X+2*unit;
            using(var brush=new SolidBrush(Color.White))e.Graphics.FillEllipse(brush,x,track.Y+2*unit,diameter,diameter);
            if(Focused&&ShowFocusCues)UiPaint.Box(e.Graphics,new RectangleF(0.5f,0.5f,Width-1,Height-1),Height/2,Color.Transparent,Color.FromArgb(128,128,124));
        }
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){keyDown=false;base.OnLostFocus(e);Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left)Focus();base.OnMouseDown(e);}
        protected override bool IsInputKey(Keys key){return key==Keys.Space||key==Keys.Enter||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){keyDown=true;e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
        protected override void OnKeyUp(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){bool click=keyDown;keyDown=false;e.Handled=true;if(click)OnClick(EventArgs.Empty);}base.OnKeyUp(e);}
        protected override AccessibleObject CreateAccessibilityInstance(){return new ToggleAccessibility(this);}
        sealed class ToggleAccessibility:ControlAccessibleObject {
            readonly VoiceToggle toggle;
            internal ToggleAccessibility(VoiceToggle owner):base(owner){toggle=owner;}
            public override AccessibleRole Role {get{return AccessibleRole.CheckButton;}}
            public override AccessibleStates State {get{return base.State|AccessibleStates.Focusable|(toggle.Checked?AccessibleStates.Checked:AccessibleStates.None);}}
            public override string DefaultAction {get{return "切换";}}
            public override void DoDefaultAction(){if(toggle.Enabled)toggle.OnClick(EventArgs.Empty);}
        }
    }

    sealed class RoundedActionButton:Button {
        bool hovered,pressed;
        internal bool Primary;
        internal RoundedActionButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Default;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnMouseEnter(EventArgs e){hovered=true;base.OnMouseEnter(e);Invalidate();}
        protected override void OnMouseLeave(EventArgs e){hovered=false;pressed=false;base.OnMouseLeave(e);Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){pressed=e.Button==MouseButtons.Left;base.OnMouseDown(e);Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){pressed=false;base.OnMouseUp(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){
            using(var background=new SolidBrush(Parent==null?UiPaint.Canvas:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);
            var fill=Primary?(pressed?Color.FromArgb(31,31,30):hovered?Color.FromArgb(61,61,58):UiPaint.Ink):(pressed?Color.FromArgb(237,237,233):hovered?Color.FromArgb(246,246,243):Color.White);
            UiPaint.Box(e.Graphics,new RectangleF(0.5f,0.5f,Width-1,Height-1),6*Height/36f,fill,Primary?Color.Empty:Color.FromArgb(193,193,188));
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?(Primary?Color.White:UiPaint.Ink):SystemColors.GrayText,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),Primary?Color.White:UiPaint.Ink,fill);
        }
    }

    sealed class RoundedTextField:Panel {
        readonly TextBox editor;
        internal RoundedTextField(TextBox input){
            editor=input;BackColor=UiPaint.Canvas;TabStop=false;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            editor.BorderStyle=BorderStyle.None;editor.BackColor=Color.White;editor.Dock=DockStyle.None;editor.Margin=Padding.Empty;Controls.Add(editor);
            editor.Enter+=(o,e)=>Invalidate();editor.Leave+=(o,e)=>Invalidate();editor.FontChanged+=(o,e)=>PerformLayout();
        }
        protected override void OnLayout(LayoutEventArgs e){
            base.OnLayout(e);if(editor==null||ClientSize.Height<8)return;
            int inset=(int)Math.Round(12*ClientSize.Height/36f),height=editor.PreferredHeight;
            editor.SetBounds(inset,Math.Max(0,(ClientSize.Height-height)/2),Math.Max(1,ClientSize.Width-2*inset),height);
        }
        protected override void OnPaint(PaintEventArgs e){
            using(var background=new SolidBrush(Parent==null?UiPaint.Canvas:Parent.BackColor))e.Graphics.FillRectangle(background,ClientRectangle);
            UiPaint.Box(e.Graphics,new RectangleF(0.5f,0.5f,Width-1,Height-1),6*Height/36f,Color.White,editor.Focused?Color.FromArgb(133,133,128):UiPaint.Border);
        }
    }

    // A single buffered control owns the closed face. The model never creates a native window.
    sealed class RoundedChoice:Control {
        readonly ComboBox native=new ComboBox();ToolStripDropDown menu;bool disposed;
        public event EventHandler SelectedIndexChanged;
        public ComboBox.ObjectCollection Items {get{return native.Items;}}
        public object SelectedItem {get{return native.SelectedItem;}set{native.SelectedItem=value;}}
        public int SelectedIndex {get{return native.SelectedIndex;}set{native.SelectedIndex=value;}}
        public ComboBoxStyle DropDownStyle {get{return native.DropDownStyle;}set{native.DropDownStyle=value;}}
        public bool DroppedDown {get{return menu!=null&&menu.Visible;}set{if(value)OpenMenu();else if(menu!=null)menu.Close();}}
        public override string Text {get{return native==null?base.Text:native.Text;}set{if(native==null)base.Text=value;else native.Text=value;}}
        internal RoundedChoice(){
            Size=new Size(150,36);BackColor=UiPaint.Canvas;TabStop=true;Cursor=Cursors.Default;AccessibleRole=AccessibleRole.ComboBox;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
            native.Visible=false;native.DropDownStyle=ComboBoxStyle.DropDownList;
            native.SelectedIndexChanged+=(o,e)=>{Invalidate();OnTextChanged(EventArgs.Empty);var changed=SelectedIndexChanged;if(changed!=null)changed(this,e);};
        }
        void PaintFace(Graphics graphics){
            float scale=Height/36f;using(var background=new SolidBrush(Parent==null?UiPaint.Canvas:Parent.BackColor))graphics.FillRectangle(background,ClientRectangle);
            UiPaint.Box(graphics,new RectangleF(0.5f,0.5f,Width-1,Height-1),6*scale,Color.White,Focused||DroppedDown?Color.FromArgb(133,133,128):UiPaint.Border);
            var textBounds=new Rectangle((int)(12*scale),0,Width-(int)(46*scale),Height);
            TextRenderer.DrawText(graphics,Text,Font,textBounds,UiPaint.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            float x=Width-21*scale,y=Height/2f;graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var pen=new Pen(Color.FromArgb(105,105,101),1.5f*scale)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;graphics.DrawLines(pen,new[]{new PointF(x-4*scale,y-2*scale),new PointF(x,y+2*scale),new PointF(x+4*scale,y-2*scale)});}
        }
        void OpenMenu(){
            if(!Enabled||DroppedDown||Items.Count==0)return;Focus();if(menu!=null)menu.Dispose();
            float scale=Height/36f;int inset=(int)Math.Round(4*scale),rowHeight=(int)Math.Round(32*scale);
            menu=new ChoiceMenu(Width,inset,rowHeight,Items.Count,scale) {Font=Font};
            for(int index=0;index<Items.Count;index++){
                int choice=index;var item=new ToolStripMenuItem(Convert.ToString(Items[index])) {AutoSize=false,Size=new Size(Math.Max(1,Width-2*inset),rowHeight),Margin=Padding.Empty};
                item.Click+=(o,e)=>SelectedIndex=choice;menu.Items.Add(item);
            }
            menu.Closed+=(o,e)=>{if(!disposed){Invalidate();if(Visible&&e.CloseReason!=ToolStripDropDownCloseReason.AppClicked&&e.CloseReason!=ToolStripDropDownCloseReason.AppFocusChange)Focus();}};
            menu.Show(this,new Point(0,Height+(int)Math.Round(4*scale)));if(SelectedIndex>=0)menu.Items[SelectedIndex].Select();Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e){PaintFace(e.Graphics);}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){Focus();DroppedDown=!DroppedDown;}base.OnMouseDown(e);}
        bool HandleKey(Keys keys){
            if(Items.Count==0)return false;
            var key=keys&Keys.KeyCode;
            if(key==Keys.F4||key==Keys.Space||key==Keys.Enter||(key==Keys.Down&&(keys&Keys.Alt)!=0)){DroppedDown=!DroppedDown;return true;}
            if(key==Keys.Escape&&DroppedDown){DroppedDown=false;return true;}
            if(key==Keys.Down||key==Keys.Up){SelectedIndex=Math.Max(0,Math.Min(Items.Count-1,SelectedIndex+(key==Keys.Down?1:-1)));return true;}
            if(key==Keys.Home||key==Keys.End){SelectedIndex=key==Keys.Home?0:Items.Count-1;return true;}return false;
        }
        protected override bool ProcessCmdKey(ref Message message,Keys keys){return HandleKey(keys)||base.ProcessCmdKey(ref message,keys);}
        protected override void OnKeyDown(KeyEventArgs e){if(HandleKey(e.KeyData)){e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
        protected override AccessibleObject CreateAccessibilityInstance(){return new ChoiceAccessibility(this);}
        protected override void Dispose(bool disposing){disposed=true;if(disposing){if(menu!=null)menu.Dispose();native.Dispose();}base.Dispose(disposing);}
        sealed class ChoiceAccessibility:ControlAccessibleObject {
            readonly RoundedChoice choice;internal ChoiceAccessibility(RoundedChoice owner):base(owner){choice=owner;}
            public override AccessibleRole Role {get{return AccessibleRole.ComboBox;}}
            public override string Value {get{return choice.Text;}set{}}
            public override string DefaultAction {get{return "展开选项";}}
            public override AccessibleStates State {get{return base.State|AccessibleStates.Focusable|(choice.DroppedDown?AccessibleStates.Expanded:AccessibleStates.Collapsed);}}
            public override void DoDefaultAction(){choice.DroppedDown=!choice.DroppedDown;}
        }
        sealed class ChoiceMenu:ToolStripDropDown {
            readonly Size fixedSize;readonly float scale;
            internal ChoiceMenu(int width,int inset,int rowHeight,int count,float unit){
                scale=unit;fixedSize=new Size(width,2*inset+rowHeight*count);
                AutoSize=false;Size=fixedSize;Padding=new Padding(inset);Cursor=Cursors.Default;
                ShowItemToolTips=false;
                BackColor=Color.White;ForeColor=UiPaint.Ink;Renderer=new ChoiceMenuRenderer(unit);
                LayoutStyle=ToolStripLayoutStyle.Flow;
                var layout=(FlowLayoutSettings)LayoutSettings;layout.FlowDirection=FlowDirection.TopDown;layout.WrapContents=false;
                SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
                UpdateShape();
            }
            public override Size GetPreferredSize(Size proposedSize){return fixedSize;}
            protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(scale>0)UpdateShape();}
            void UpdateShape(){
                float diameter=12*scale;using(var path=new GraphicsPath()){
                    path.AddArc(0,0,diameter,diameter,180,90);path.AddArc(Width-diameter,0,diameter,diameter,270,90);
                    path.AddArc(Width-diameter,Height-diameter,diameter,diameter,0,90);path.AddArc(0,Height-diameter,diameter,diameter,90,90);path.CloseFigure();
                    var old=Region;Region=new Region(path);if(old!=null)old.Dispose();
                }
            }
        }
        sealed class ChoiceMenuRenderer:ToolStripProfessionalRenderer {
            readonly float scale;internal ChoiceMenuRenderer(float unit){scale=unit;RoundedEdges=false;}
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(Color.White);}
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){
                UiPaint.Box(e.Graphics,new RectangleF(0.5f,0.5f,e.Item.Width-1,e.Item.Height-1),4*scale,e.Item.Selected?Color.FromArgb(239,239,235):Color.White,Color.Empty);
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
                TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,new Rectangle((int)Math.Round(8*scale),0,e.Item.Width-(int)Math.Round(16*scale),e.Item.Height),UiPaint.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){UiPaint.Box(e.Graphics,new RectangleF(0.5f,0.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),6*scale,Color.Transparent,UiPaint.Border);}
        }
    }
}
