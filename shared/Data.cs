using System;using System.IO;using System.Runtime.Serialization.Json;using System.Text;
namespace CheerCustom {
    static class Data {
        public static string Root=AppDomain.CurrentDomain.BaseDirectory,Dir=Path.Combine(Root,"user-data");
        public static T Read<T>(string path){using(var stream=File.OpenRead(path))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
        public static string Json<T>(T value){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
        public static void Save<T>(string path,T value){Directory.CreateDirectory(Path.GetDirectoryName(path));var temporary=path+".tmp";File.WriteAllText(temporary,Json(value),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);}
    }
}