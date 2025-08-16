using System.Buffers;
using System.IO.Compression;
using System.Text;

namespace Application.Utils;

public static class CompressingEncoding
{
    public static byte[] Compress(this string? message)
    {
        if (string.IsNullOrEmpty(message))
            return Array.Empty<byte>();
        
        var encoding = Encoding.UTF8;
        var maxByteCount = encoding.GetMaxByteCount(message.Length);
        var rentedBuffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        
        try
        {
            var actualByteCount = encoding.GetBytes(message, 0, message.Length, rentedBuffer, 0);
            
            using var compressedStream = new MemoryStream();
            using (var zipStream = new GZipStream(compressedStream, CompressionLevel.Optimal))
            {
                zipStream.Write(rentedBuffer, 0, actualByteCount);
            }
            return compressedStream.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }
    
    public static string Decompress(this byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        using var compressedStream = new MemoryStream(bytes);
        using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        
        // Используем буферизованное чтение вместо CopyTo в MemoryStream
        using var reader = new StreamReader(zipStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
    
    
    public static string EncodeBase64(this byte[]? bytes)
    {
        return Convert.ToBase64String(bytes ?? Array.Empty<byte>());
    }
    
    public static byte[] DecodeBase64(this string? base64String)
    {
        return Convert.FromBase64String(base64String ?? string.Empty);
    }
}