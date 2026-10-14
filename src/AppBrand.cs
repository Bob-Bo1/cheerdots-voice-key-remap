using System.Drawing;
using System.Reflection;

[assembly:AssemblyVersion("0.1.0.0")]
[assembly:AssemblyFileVersion("0.1.0.0")]
[assembly:AssemblyTitle("语音改建工具")]
[assembly:AssemblyProduct("语音改建工具")]
[assembly:AssemblyDescription("Cheerdots 语音按键自定义工具")]

namespace CheerVoiceLite {
    static class AppBrand {
        internal const string Name="语音改建工具";
        internal static Icon LoadIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("VoiceTool.Icon"))using(var icon=new Icon(stream))return (Icon)icon.Clone();}
    }
}
