using System.Text;

namespace Bosbase.Models;

/// <summary>
/// Represents a multipart file attachment field.
/// </summary>
public class FileAttachment
{
    public string FieldName { get; }
    public Stream Content { get; }
    public string FileName { get; }
    public string ContentType { get; }

    public FileAttachment(string fieldName, Stream content, string fileName, string? contentType = null)
    {
        FieldName = fieldName;
        Content = content;
        FileName = string.IsNullOrWhiteSpace(fileName) ? fieldName : fileName;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType!;
    }

    public static FileAttachment FromBytes(string fieldName, byte[] data, string fileName, string? contentType = null)
    {
        return new FileAttachment(fieldName, new MemoryStream(data), fileName, contentType);
    }

    public static FileAttachment FromString(string fieldName, string value, string fileName, string? contentType = "text/plain")
    {
        return new FileAttachment(fieldName, new MemoryStream(Encoding.UTF8.GetBytes(value)), fileName, contentType);
    }

    public static FileAttachment FromPath(string fieldName, string path, string? contentType = null)
    {
        var stream = File.OpenRead(path);
        var name = Path.GetFileName(path);
        return new FileAttachment(fieldName, stream, name, contentType);
    }
}
