namespace DZ_MinIO.Models;

public class FileItem
{
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
}