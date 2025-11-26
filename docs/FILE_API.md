# File API - C# SDK Documentation

## Overview

The File API provides endpoints for downloading and accessing files stored in collection records. It supports thumbnail generation for images, protected file access with tokens, and force download options.

**Key Features:**
- Download files from collection records
- Generate thumbnails for images (crop, fit, resize)
- Protected file access with short-lived tokens
- Force download option for any file type
- Automatic content-type detection
- Support for Range requests and caching

**Backend Endpoints:**
- `GET /api/files/{collection}/{recordId}/{filename}` - Download/fetch file
- `POST /api/files/token` - Generate protected file token

## Download / Fetch File

Downloads a single file resource from a record.

### Basic Usage

```csharp
using Bosbase;

var pb = new BosbaseClient("http://127.0.0.1:8090");

// Get a record with a file field
var record = await pb.Collection("posts").GetOneAsync("RECORD_ID");

// Get the file URL
var image = record["image"]?.ToString();
var fileUrl = pb.Files.GetUrl(record, image ?? "");

// Use the URL (e.g., in a web browser or download)
Console.WriteLine($"File URL: {fileUrl}");
```

### File URL Structure

The file URL follows this pattern:
```
/api/files/{collectionIdOrName}/{recordId}/{filename}
```

Example:
```
http://127.0.0.1:8090/api/files/posts/abc123/photo_xyz789.jpg
```

### Using in Web Applications

```html
<!-- Direct image display -->
<img src="http://127.0.0.1:8090/api/files/posts/abc123/photo_xyz789.jpg" alt="Photo" />

<!-- Download link -->
<a href="http://127.0.0.1:8090/api/files/posts/abc123/document.pdf" download>Download PDF</a>

<!-- Video player -->
<video src="http://127.0.0.1:8090/api/files/posts/abc123/video.mp4" controls></video>
```

## Thumbnails

Generate thumbnails for image files on-the-fly.

### Thumbnail Formats

The following thumbnail formats are supported:

| Format | Example | Description |
|--------|---------|-------------|
| `WxH` | `100x300` | Crop to WxH viewbox (from center) |
| `WxHt` | `100x300t` | Crop to WxH viewbox (from top) |
| `WxHb` | `100x300b` | Crop to WxH viewbox (from bottom) |
| `WxHf` | `100x300f` | Fit inside WxH viewbox (without cropping) |
| `0xH` | `0x300` | Resize to H height preserving aspect ratio |
| `Wx0` | `100x0` | Resize to W width preserving aspect ratio |

### Using Thumbnails

```csharp
var record = await pb.Collection("example").GetOneAsync("RECORD_ID");
var image = record["image"]?.ToString();

if (image != null)
{
    // Get thumbnail URL
    var thumbUrl = pb.Files.GetUrl(record, image, thumb: "100x100");

    // Different thumbnail sizes
    var smallThumb = pb.Files.GetUrl(record, image, thumb: "50x50");
    var mediumThumb = pb.Files.GetUrl(record, image, thumb: "200x200");
    var largeThumb = pb.Files.GetUrl(record, image, thumb: "500x500");

    // Fit thumbnail (no cropping)
    var fitThumb = pb.Files.GetUrl(record, image, thumb: "200x200f");

    // Resize to specific width
    var widthThumb = pb.Files.GetUrl(record, image, thumb: "300x0");

    // Resize to specific height
    var heightThumb = pb.Files.GetUrl(record, image, thumb: "0x200");
}
```

### Thumbnail Examples in HTML

```html
<!-- Small thumbnail -->
<img src="http://127.0.0.1:8090/api/files/posts/abc123/photo.jpg?thumb=100x100" alt="Thumbnail" />

<!-- Medium thumbnail with fit -->
<img src="http://127.0.0.1:8090/api/files/posts/abc123/photo.jpg?thumb=300x300f" alt="Photo" />

<!-- Responsive thumbnail -->
<img 
  src="http://127.0.0.1:8090/api/files/posts/abc123/photo.jpg?thumb=400x400" 
  srcset="
    http://127.0.0.1:8090/api/files/posts/abc123/photo.jpg?thumb=200x200 200w,
    http://127.0.0.1:8090/api/files/posts/abc123/photo.jpg?thumb=400x400 400w,
    http://127.0.0.1:8090/api/files/posts/abc123/photo.jpg?thumb=800x800 800w
  "
  sizes="(max-width: 600px) 200px, (max-width: 1200px) 400px, 800px"
  alt="Responsive image"
/>
```

### Thumbnail Behavior

- **Image Files Only**: Thumbnails are only generated for image files (PNG, JPG, JPEG, GIF, WEBP)
- **Non-Image Files**: For non-image files, the thumb parameter is ignored and the original file is returned
- **Caching**: Thumbnails are cached and reused if already generated
- **Fallback**: If thumbnail generation fails, the original file is returned
- **Field Configuration**: Thumb sizes must be defined in the file field's `thumbs` option or use default `100x100`

## Protected Files

Protected files require a special token for access, even if you're authenticated.

### Getting a File Token

```csharp
// Must be authenticated first
await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password");

// Get file token
var token = await pb.Files.GetTokenAsync();

Console.WriteLine(token); // Short-lived JWT token
```

### Using Protected File Token

```csharp
// Get protected file URL with token
var record = await pb.Collection("example").GetOneAsync("RECORD_ID");
var document = record["privateDocument"]?.ToString();

if (document != null)
{
    var protectedFileUrl = pb.Files.GetUrl(record, document, token: token);

    // Use the URL (e.g., download or display)
    Console.WriteLine($"Protected file URL: {protectedFileUrl}");
}
```

### Protected File Example

```csharp
async Task<string?> DisplayProtectedImageAsync(string recordId)
{
    // Authenticate
    await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password");
    
    // Get record
    var record = await pb.Collection("documents").GetOneAsync(recordId);
    
    // Get file token
    var token = await pb.Files.GetTokenAsync();
    
    // Get protected file URL
    var thumbnail = record["thumbnail"]?.ToString();
    if (thumbnail != null)
    {
        var imageUrl = pb.Files.GetUrl(record, thumbnail, token: token, thumb: "300x300");
        return imageUrl;
    }
    
    return null;
}
```

### Token Lifetime

- File tokens are short-lived (typically expires after a few minutes)
- Tokens are associated with the authenticated user/superuser
- Generate a new token if the previous one expires

## Force Download

Force files to download instead of being displayed in the browser.

```csharp
var record = await pb.Collection("example").GetOneAsync("RECORD_ID");
var document = record["document"]?.ToString();

if (document != null)
{
    // Force download
    var downloadUrl = pb.Files.GetUrl(record, document, download: true);
    Console.WriteLine($"Download URL: {downloadUrl}");
}
```

### Download Parameter Values

```csharp
// These all force download:
pb.Files.GetUrl(record, filename, download: true);

// These allow inline display (default):
pb.Files.GetUrl(record, filename, download: false);
pb.Files.GetUrl(record, filename); // No download parameter
```

## Complete Examples

### Example 1: Image Gallery

```csharp
async Task DisplayImageGalleryAsync(string recordId)
{
    var record = await pb.Collection("posts").GetOneAsync(recordId);
    
    var images = record["images"] as List<object?> ?? new List<object?>();
    if (images.Count == 0 && record["image"] != null)
    {
        images = new List<object?> { record["image"] };
    }
    
    foreach (var imageObj in images)
    {
        var filename = imageObj?.ToString();
        if (filename == null) continue;

        // Thumbnail for gallery
        var thumbUrl = pb.Files.GetUrl(record, filename, thumb: "200x200");
        
        // Full image URL
        var fullUrl = pb.Files.GetUrl(record, filename);
        
        Console.WriteLine($"Thumbnail: {thumbUrl}");
        Console.WriteLine($"Full image: {fullUrl}");
    }
}
```

### Example 2: File Download Handler

```csharp
async Task<string> GetDownloadUrlAsync(string recordId, string filename)
{
    var record = await pb.Collection("documents").GetOneAsync(recordId);
    
    // Get download URL
    var downloadUrl = pb.Files.GetUrl(record, filename, download: true);
    
    return downloadUrl;
}
```

### Example 3: Protected File Viewer

```csharp
async Task<string?> ViewProtectedFileAsync(string recordId)
{
    // Authenticate
    if (!pb.AuthStore.IsValid())
    {
        await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password");
    }
    
    // Get record
    var record = await pb.Collection("private_docs").GetOneAsync(recordId);
    
    // Get token
    string? token;
    try
    {
        token = await pb.Files.GetTokenAsync();
    }
    catch (Exception error)
    {
        Console.Error.WriteLine($"Failed to get file token: {error}");
        return null;
    }
    
    // Get file URL
    var file = record["file"]?.ToString();
    if (file != null)
    {
        var fileUrl = pb.Files.GetUrl(record, file, token: token);
        return fileUrl;
    }
    
    return null;
}
```

### Example 4: Multiple Files with Thumbnails

```csharp
async Task DisplayFileListAsync(string recordId)
{
    var record = await pb.Collection("attachments").GetOneAsync(recordId);
    
    var files = record["files"] as List<object?> ?? new List<object?>();
    
    foreach (var fileObj in files)
    {
        var filename = fileObj?.ToString();
        if (filename == null) continue;
        
        // Check if it's an image
        var ext = Path.GetExtension(filename).ToLower();
        var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }.Contains(ext);
        
        if (isImage)
        {
            // Show thumbnail
            var thumbUrl = pb.Files.GetUrl(record, filename, thumb: "100x100");
            Console.WriteLine($"Thumbnail: {thumbUrl}");
        }
        
        // File download link
        var downloadUrl = pb.Files.GetUrl(record, filename, download: true);
        Console.WriteLine($"Download: {downloadUrl}");
    }
}
```

## Error Handling

```csharp
try
{
    var record = await pb.Collection("posts").GetOneAsync("RECORD_ID");
    var image = record["image"]?.ToString();
    
    if (string.IsNullOrEmpty(image))
    {
        throw new Exception("Invalid file URL");
    }
    
    var fileUrl = pb.Files.GetUrl(record, image);
    
    // Use the URL
    Console.WriteLine($"File URL: {fileUrl}");
    
}
catch (Exception error)
{
    Console.Error.WriteLine($"File access error: {error}");
}
```

### Protected File Token Error Handling

```csharp
async Task<string?> GetProtectedFileUrlAsync(Dictionary<string, object?> record, string filename)
{
    try
    {
        // Get token
        var token = await pb.Files.GetTokenAsync();
        
        // Get file URL
        return pb.Files.GetUrl(record, filename, token: token);
        
    }
    catch (ClientResponseError error)
    {
        if (error.Status == 401)
        {
            Console.Error.WriteLine("Not authenticated");
            // Redirect to login
        }
        else if (error.Status == 403)
        {
            Console.Error.WriteLine("No permission to access file");
        }
        else
        {
            Console.Error.WriteLine($"Failed to get file token: {error}");
        }
        return null;
    }
}
```

## Best Practices

1. **Use Thumbnails for Lists**: Use thumbnails when displaying images in lists/grids to reduce bandwidth
2. **Lazy Loading**: Use lazy loading for images below the fold
3. **Cache Tokens**: Store file tokens and reuse them until they expire
4. **Error Handling**: Always handle file loading errors gracefully
5. **Content-Type**: Let the server handle content-type detection automatically
6. **Range Requests**: The API supports Range requests for efficient video/audio streaming
7. **Caching**: Files are cached with a 30-day cache-control header
8. **Security**: Always use tokens for protected files, never expose them in client-side code

## Thumbnail Size Guidelines

| Use Case | Recommended Size |
|----------|-----------------|
| Profile picture | `100x100` or `150x150` |
| List thumbnails | `200x200` or `300x300` |
| Card images | `400x400` or `500x500` |
| Gallery previews | `300x300f` (fit) or `400x400f` |
| Hero images | Use original or `800x800f` |
| Avatar | `50x50` or `75x75` |

## Limitations

- **Thumbnails**: Only work for image files (PNG, JPG, JPEG, GIF, WEBP)
- **Protected Files**: Require authentication to get tokens
- **Token Expiry**: File tokens expire after a short period (typically minutes)
- **File Size**: Large files may take time to generate thumbnails on first request
- **Thumb Sizes**: Must match sizes defined in field configuration or use default `100x100`

## Related Documentation

- [Files Upload and Handling](./FILES.md) - Uploading and managing files
- [API Records](./API_RECORDS.md) - Working with records
- [Collections](./COLLECTIONS.md) - Collection configuration

