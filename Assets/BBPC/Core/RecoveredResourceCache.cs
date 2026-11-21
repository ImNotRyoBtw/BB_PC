using System;
using System.IO;
using System.Security.Cryptography;
namespace BBPC.Gameplay
{
    // Native CacheManager.Read 0x2C98178 validates the downloaded (still encrypted) bytes.
    // Windows owns a separate cache directory; it never invalidates original APK/captured samples.
    public sealed class RecoveredResourceCache
    {
        private readonly string directory;
        public RecoveredResourceCache(string directory) { this.directory=Path.GetFullPath(directory); Directory.CreateDirectory(this.directory); }
        private string PathFor(string md5,string extension)
        {
            if(md5==null || md5.Length!=32) throw new ArgumentException("Expected original remote MD5");
            foreach(char c in md5) if(!((c>='0'&&c<='9')||(c>='a'&&c<='f'))) throw new ArgumentException("Expected lowercase hexadecimal remote MD5");
            if(extension!="bpm" && extension!="bpmd") throw new ArgumentException("Unrecovered content extension");
            return Path.Combine(directory,md5+"."+extension);
        }
        public static string Md5(byte[] bytes) { using(var md5=MD5.Create()) return BitConverter.ToString(md5.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        public void Write(string expectedMd5,string extension,byte[] transportedBytes)
        {
            string path=PathFor(expectedMd5,extension);
            if(transportedBytes==null || transportedBytes.Length==0) return;
            File.WriteAllBytes(path,transportedBytes);
        }
        public byte[] Read(string expectedMd5,string extension)
        {
            string path=PathFor(expectedMd5,extension);
            if(!File.Exists(path)) return null;
            byte[] bytes=File.ReadAllBytes(path);
            if(bytes.Length==0) return null; // Native empty file returns null without deleting it here.
            if(Md5(bytes)==expectedMd5) return bytes;
            File.Delete(path); return null;
        }
        public void Invalidate(string expectedMd5,string extension)
        { string path=PathFor(expectedMd5,extension); if(File.Exists(path)) File.Delete(path); }
    }
}
