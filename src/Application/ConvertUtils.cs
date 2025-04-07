using System.IO.Compression;
using System.Text;

namespace Application;

public static class ConvertUtils
{
    public static string EncodeToBase64(this string plainText) 
        => plainText.ToByteArray().EncodeToBase64();
    
    public static string EncodeToBase64(this byte[] byteArray) 
        => Convert.ToBase64String(byteArray);
    
    public static string DecodeFromBase64(this string base64EncodedData) 
        => Convert.FromBase64String(base64EncodedData).GetString();
    

    public static byte[] CompressToGzip(this string plainText)
    {
        var bytes = plainText.ToByteArray();
        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        return memoryStream.ToArray();
    }
    
    public static string DecompressFromGzip(this byte[] gzipBytes)
    {
        using var memoryStream = new MemoryStream(gzipBytes);
        using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzipStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    
    public static byte[] ToByteArray(this string plainText) => Encoding.UTF8.GetBytes(plainText);
    
    public static string GetString(this byte[] bytes) => Encoding.UTF8.GetString(bytes);
}