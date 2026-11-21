using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace BBPC.UnityRuntime
{
    // Transport integrity precedes the already-verified basename/AES/XOR decoder.
    public static class RecoveredContentBytes
    {
        public static string Hash(byte[] bytes,bool md5=false)
        {using(HashAlgorithm hash=md5?(HashAlgorithm)MD5.Create():SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public static byte[] Decode(RemoteSongFile location,byte[] encrypted)
        {
            if(!RecoveredReadContracts.ValidRemoteFile(location)||Hash(encrypted,true)!=location.md5.ToLowerInvariant())throw new InvalidDataException("Transport MD5 mismatch");
            string name=Uri.UnescapeDataString(new Uri(location.url).Segments[new Uri(location.url).Segments.Length-1]);
            if(string.IsNullOrEmpty(name)||name.IndexOfAny(new[]{'/', '\\'})>=0)throw new FormatException("Invalid original resource basename");
            byte[] keyName=Encoding.UTF8.GetBytes(name),plain;
            using(var aes=Aes.Create())
            {aes.Key=Encoding.ASCII.GetBytes(Hash(keyName,true));aes.Mode=CipherMode.ECB;aes.Padding=PaddingMode.PKCS7;using(var decoder=aes.CreateDecryptor())plain=decoder.TransformFinalBlock(encrypted,0,encrypted.Length);}
            int sum=0;foreach(byte value in keyName)sum=unchecked(sum+value);for(int i=0;i<plain.Length;i++)plain[i]=(byte)(plain[i]^(sum&255));return plain;
        }
    }
}
