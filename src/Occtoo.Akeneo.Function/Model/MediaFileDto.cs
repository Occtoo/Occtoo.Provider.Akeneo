namespace Occtoo.Akeneo.Function.Model;

public class StorageLocation
{
    public string HostName { get; set; }
    public string ContainerName { get; set; }
    public string FileName { get; set; }
}

public class FileMetadata
{
    public string Filename { get; set; }
    public string MimeType { get; set; }
    public long Size { get; set; }
    public string Extension { get; set; }
    public MediaInfo MediaInfo { get; set; }
}

public class MediaInfo
{
    public VideoMetadata Video { get; set; }
    public ImageMetadata Image { get; set; }
}

public class VideoMetadata
{
    public int DurationInSeconds { get; set; }
    public AspectRatio AspectRatio { get; set; }
}

public class AspectRatio
{
    public int Height { get; set; }
    public int Width { get; set; }
}
public class ImageMetadata
{
    public int Width { get; set; }
    public int Height { get; set; }
    public Resolution Resolution { get; set; }
}

public class Resolution
{
    public double Vertical { get; set; }
    public double Horizontal { get; set; }
}