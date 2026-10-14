$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.IO;using System.Collections.Generic;
public static class VoiceToolIconBuilder {
    public static void Build(string path){var images=new List<byte[]>();var sizes=new[]{16,20,24,32,48,64,128,256};foreach(int size in sizes)using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bitmap)){
        g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/64f,size/64f);g.Clear(Color.Transparent);
        using(var shape=new GraphicsPath()){shape.AddArc(1,1,20,20,180,90);shape.AddArc(43,1,20,20,270,90);shape.AddArc(43,43,20,20,0,90);shape.AddArc(1,43,20,20,90,90);shape.CloseFigure();using(var brush=new SolidBrush(Color.FromArgb(43,43,41)))g.FillPath(brush,shape);}
        using(var brush=new SolidBrush(Color.White))using(var capsule=new GraphicsPath()){capsule.AddArc(25,13,14,14,180,180);capsule.AddArc(25,25,14,14,0,180);capsule.CloseFigure();g.FillPath(brush,capsule);}
        using(var pen=new Pen(Color.White,3.6f)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawArc(pen,19,20,26,24,0,180);g.DrawLine(pen,19,32,19,27);g.DrawLine(pen,45,32,45,27);g.DrawLine(pen,32,44,32,51);g.DrawLine(pen,25,51,39,51);}
        // DIB frames also support .NET Framework's icon rendering/conversion paths.
        using(var dib=new MemoryStream())using(var writer=new BinaryWriter(dib)){
            writer.Write(40);writer.Write(size);writer.Write(size*2);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(0);writer.Write(size*size*4);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
            for(int y=size-1;y>=0;y--)for(int x=0;x<size;x++){var pixel=bitmap.GetPixel(x,y);writer.Write(pixel.B);writer.Write(pixel.G);writer.Write(pixel.R);writer.Write(pixel.A);}
            int stride=((size+31)/32)*4;for(int y=size-1;y>=0;y--){var mask=new byte[stride];for(int x=0;x<size;x++)if(bitmap.GetPixel(x,y).A<128)mask[x/8]|=(byte)(128>>(x%8));writer.Write(mask);}images.Add(dib.ToArray());
        }
    }using(var file=new BinaryWriter(File.Create(path))){file.Write((ushort)0);file.Write((ushort)1);file.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((byte)(sizes[i]==256?0:sizes[i]));file.Write((byte)0);file.Write((byte)0);file.Write((ushort)1);file.Write((ushort)32);file.Write(images[i].Length);file.Write(offset);offset+=images[i].Length;}foreach(var png in images)file.Write(png);}}
}
'@
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'assets')|Out-Null
[VoiceToolIconBuilder]::Build((Join-Path $PSScriptRoot 'assets/voice-tool.ico'))
