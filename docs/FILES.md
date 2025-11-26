# Files Upload and Handling - C# SDK Documentation

## Overview

BosBase allows you to upload and manage files through file fields in your collections. Files are stored with sanitized names and a random suffix for security (e.g., `test_52iwbgds7l.png`).

**Key Features:**
- Upload multiple files per field
- Maximum file size: ~8GB (2^53-1 bytes)
- Automatic filename sanitization and random suffix
- Image thumbnails support
- Protected files with token-based access
- File modifiers for append/prepend/delete operations

**Backend Endpoints:**
- `POST /api/files/token` - Get file access token for protected files
- `GET /api/files/{collection}/{recordId}/{filename}` - Download file

## File Field Configuration

Before uploading files, you must add a file field to your collection:

```csharp
var collection = await pb.Collections.GetOneAsync("example");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

fields.Add(new Dictionary<string, object?>
{
    ["name"] = "documents",
    ["type"] = "file",
    ["maxSelect"] = 5,        // Maximum number of files (1 for single file)
    ["maxSize"] = 5242880,     // 5MB in bytes (optional, default: 5MB)
    ["mimeTypes"] = new[] { "image/jpeg", "image/png", "application/pdf" },
    ["thumbs"] = new[] { "100x100", "300x300" },  // Thumbnail sizes for images
    ["protected"] = false      // Require token for access
});

await pb.Collections.UpdateAsync("example", new Dictionary<string, object?>
{
    ["fields"] = fields
});
```

## Uploading Files

### Basic Upload with Create

When creating a new record, you can upload files directly:

```csharp
using Bosbase;
using Bosbase.Models;

var pb = new BosbaseClient("http://localhost:8090");

// Method 1: Using FileAttachment objects
var file1 = new FileAttachment
{
    FieldName = "documents",
    FileName = "file1.txt",
    Content = Encoding.UTF8.GetBytes("content 1..."),
    ContentType = "text/plain"
};

var file2 = new FileAttachment
{
    FieldName = "documents",
    FileName = "file2.txt",
    Content = Encoding.UTF8.GetBytes("content 2..."),
    ContentType = "text/plain"
};

var createdRecord = await pb.Collection("example").CreateAsync(
    new Dictionary<string, object?> { ["title"] = "Hello world!" },
    files: new[] { file1, file2 }
);
```

### Upload with Update

```csharp
// Update record and upload new files
var file3 = new FileAttachment
{
    FieldName = "documents",
    FileName = "file3.txt",
    Content = Encoding.UTF8.GetBytes("content 3..."),
    ContentType = "text/plain"
};

var updatedRecord = await pb.Collection("example").UpdateAsync(
    "RECORD_ID",
    new Dictionary<string, object?> { ["title"] = "Updated title" },
    files: new[] { file3 }
);
```

### Append Files (Using + Modifier)

For multiple file fields, use the `+` modifier to append files:

```csharp
// Append files to existing ones
var file4 = new FileAttachment
{
    FieldName = "documents+",  // Note the + modifier
    FileName = "file4.txt",
    Content = Encoding.UTF8.GetBytes("content 4..."),
    ContentType = "text/plain"
};

await pb.Collection("example").UpdateAsync(
    "RECORD_ID",
    new Dictionary<string, object?>(),
    files: new[] { file4 }
);

// Or prepend files (files will appear first)
var file0 = new FileAttachment
{
    FieldName = "+documents",  // Note the + prefix
    FileName = "file0.txt",
    Content = Encoding.UTF8.GetBytes("content 0..."),
    ContentType = "text/plain"
};

await pb.Collection("example").UpdateAsync(
    "RECORD_ID",
    new Dictionary<string, object?>(),
    files: new[] { file0 }
);
```

### Upload Multiple Files with Modifiers

```csharp
var files = new List<FileAttachment>();
foreach (var selectedFile in selectedFiles)
{
    files.Add(new FileAttachment
    {
        FieldName = "documents+",  // Append modifier
        FileName = selectedFile.FileName,
        Content = selectedFile.Content,
        ContentType = selectedFile.ContentType
    });
}

await pb.Collection("example").UpdateAsync(
    "RECORD_ID",
    new Dictionary<string, object?> { ["title"] = "Updated" },
    files: files
);
```

## Deleting Files

### Delete All Files

```csharp
// Delete all files in a field (set to empty array)
await pb.Collection("example").UpdateAsync("RECORD_ID", new Dictionary<string, object?>
{
    ["documents"] = new List<object>()
});
```

### Delete Specific Files (Using - Modifier)

```csharp
// Delete individual files by filename
await pb.Collection("example").UpdateAsync("RECORD_ID", new Dictionary<string, object?>
{
    ["documents-"] = new[] { "file1.pdf", "file2.txt" }
});
```

## File URLs

### Get File URL

Each uploaded file can be accessed via its URL:

```
http://localhost:8090/api/files/COLLECTION_ID_OR_NAME/RECORD_ID/FILENAME
```

**Using SDK:**

```csharp
var record = await pb.Collection("example").GetOneAsync("RECORD_ID");

// Single file field (returns string)
var filename = record["documents"]?.ToString();
var url = pb.Files.GetUrl(record, filename ?? "");

// Multiple file field (returns array)
var documents = record["documents"] as List<object?>;
var firstFile = documents?.FirstOrDefault()?.ToString();
if (firstFile != null)
{
    var url = pb.Files.GetUrl(record, firstFile);
}
```

### Image Thumbnails

If your file field has thumbnail sizes configured, you can request thumbnails:

```csharp
var record = await pb.Collection("example").GetOneAsync("RECORD_ID");
var filename = record["avatar"]?.ToString();  // Image file

// Get thumbnail with specific size
var thumbUrl = pb.Files.GetUrl(record, filename ?? "", thumb: "100x300");  // Width x Height
```

**Thumbnail Formats:**

- `WxH` (e.g., `100x300`) - Crop to WxH viewbox from center
- `WxHt` (e.g., `100x300t`) - Crop to WxH viewbox from top
- `WxHb` (e.g., `100x300b`) - Crop to WxH viewbox from bottom
- `WxHf` (e.g., `100x300f`) - Fit inside WxH viewbox (no cropping)
- `0xH` (e.g., `0x300`) - Resize to H height, preserve aspect ratio
- `Wx0` (e.g., `100x0`) - Resize to W width, preserve aspect ratio

**Supported Image Formats:**
- JPEG (`.jpg`, `.jpeg`)
- PNG (`.png`)
- GIF (`.gif` - first frame only)
- WebP (`.webp` - stored as PNG)

**Example:**

```csharp
var record = await pb.Collection("products").GetOneAsync("PRODUCT_ID");
var image = record["image"]?.ToString();

if (image != null)
{
    // Different thumbnail sizes
    var thumbSmall = pb.Files.GetUrl(record, image, thumb: "100x100");
    var thumbMedium = pb.Files.GetUrl(record, image, thumb: "300x300f");
    var thumbLarge = pb.Files.GetUrl(record, image, thumb: "800x600");
    var thumbHeight = pb.Files.GetUrl(record, image, thumb: "0x400");
    var thumbWidth = pb.Files.GetUrl(record, image, thumb: "600x0");
}
```

### Force Download

To force browser download instead of preview:

```csharp
var url = pb.Files.GetUrl(record, filename ?? "", download: true);
```

## Protected Files

By default, all files are publicly accessible if you know the full URL. For sensitive files, you can mark the field as "Protected" in the collection settings.

### Setting Up Protected Files

```csharp
var collection = await pb.Collections.GetOneAsync("example");
var fields = (collection["fields"] as List<object>)?.Cast<Dictionary<string, object?>>().ToList() 
    ?? new List<Dictionary<string, object?>>();

var fileField = fields.FirstOrDefault(f => f["name"]?.ToString() == "documents");
if (fileField != null)
{
    fileField["protected"] = true;
    await pb.Collections.UpdateAsync("example", new Dictionary<string, object?>
    {
        ["fields"] = fields
    });
}
```

### Accessing Protected Files

Protected files require authentication and a file token:

```csharp
// Step 1: Authenticate
await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "password123");

// Step 2: Get file token (valid for ~2 minutes)
var fileToken = await pb.Files.GetTokenAsync();

// Step 3: Get protected file URL with token
var record = await pb.Collection("example").GetOneAsync("RECORD_ID");
var privateDoc = record["privateDocument"]?.ToString();
if (privateDoc != null)
{
    var url = pb.Files.GetUrl(record, privateDoc, token: fileToken);
    
    // Use the URL (e.g., in a web browser or download)
    Console.WriteLine($"Protected file URL: {url}");
}
```

**Important:**
- File tokens are short-lived (~2 minutes)
- Only authenticated users satisfying the collection's `viewRule` can access protected files
- Tokens must be regenerated when they expire

### Complete Protected File Example

```csharp
async Task<string?> LoadProtectedImageAsync(string recordId, string filename)
{
    try
    {
        // Check if authenticated
        if (!pb.AuthStore.IsValid())
        {
            throw new Exception("Not authenticated");
        }

        // Get fresh token
        var token = await pb.Files.GetTokenAsync();

        // Get file URL
        var record = await pb.Collection("example").GetOneAsync(recordId);
        var url = pb.Files.GetUrl(record, filename, token: token);

        return url;
    }
    catch (ClientResponseError err)
    {
        if (err.Status == 404)
        {
            Console.Error.WriteLine("File not found or access denied");
        }
        else if (err.Status == 401)
        {
            Console.Error.WriteLine("Authentication required");
            pb.AuthStore.Clear();
        }
        throw;
    }
}
```

## Complete Examples

### Example 1: Image Upload with Thumbnails

```csharp
using Bosbase;
using Bosbase.Models;

var pb = new BosbaseClient("http://localhost:8090");
await pb.Collection("_superusers").AuthWithPasswordAsync("admin@example.com", "password");

// Create collection with image field and thumbnails
var collection = await pb.Collections.CreateBaseAsync("products", new Dictionary<string, object?>
{
    ["fields"] = new List<Dictionary<string, object?>>
    {
        new Dictionary<string, object?> { ["name"] = "name", ["type"] = "text", ["required"] = true },
        new Dictionary<string, object?>
        {
            ["name"] = "image",
            ["type"] = "file",
            ["maxSelect"] = 1,
            ["mimeTypes"] = new[] { "image/jpeg", "image/png" },
            ["thumbs"] = new[] { "100x100", "300x300", "800x600f" }  // Thumbnail sizes
        }
    }
});

// Upload product with image
var imageBytes = File.ReadAllBytes("product.jpg");
var fileAttachment = new FileAttachment
{
    FieldName = "image",
    FileName = "product.jpg",
    Content = imageBytes,
    ContentType = "image/jpeg"
};

var product = await pb.Collection("products").CreateAsync(
    new Dictionary<string, object?> { ["name"] = "My Product" },
    files: new[] { fileAttachment }
);

// Display thumbnail in UI
var imageFilename = product["image"]?.ToString();
if (imageFilename != null)
{
    var thumbnailUrl = pb.Files.GetUrl(product, imageFilename, thumb: "300x300");
    Console.WriteLine($"Thumbnail URL: {thumbnailUrl}");
}
```

### Example 2: Multiple File Upload

```csharp
async Task UploadMultipleFilesAsync(List<FileInfo> files)
{
    var fileAttachments = files.Select(file => new FileAttachment
    {
        FieldName = "documents",
        FileName = file.Name,
        Content = File.ReadAllBytes(file.FullName),
        ContentType = GetContentType(file.Extension)
    }).ToList();

    try
    {
        var record = await pb.Collection("example").CreateAsync(
            new Dictionary<string, object?> { ["title"] = "Document Set" },
            files: fileAttachments
        );
        
        Console.WriteLine("Uploaded files successfully");
    }
    catch (Exception err)
    {
        Console.Error.WriteLine($"Upload failed: {err}");
    }
}

string GetContentType(string extension)
{
    return extension.ToLower() switch
    {
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream"
    };
}
```

### Example 3: File Management

```csharp
class FileManager
{
    private readonly BosbaseClient _pb;
    private readonly string _collectionId;
    private readonly string _recordId;
    private Dictionary<string, object?>? _record;

    public FileManager(BosbaseClient pb, string collectionId, string recordId)
    {
        _pb = pb;
        _collectionId = collectionId;
        _recordId = recordId;
    }

    public async Task LoadAsync()
    {
        _record = await _pb.Collection(_collectionId).GetOneAsync(_recordId);
    }

    public List<string> GetFiles()
    {
        if (_record == null) return new List<string>();
        
        var documents = _record["documents"];
        if (documents is List<object?> files)
        {
            return files.Select(f => f?.ToString() ?? "").Where(f => !string.IsNullOrEmpty(f)).ToList();
        }
        else if (documents is string singleFile)
        {
            return new List<string> { singleFile };
        }
        
        return new List<string>();
    }

    public async Task DeleteFileAsync(string filename)
    {
        await _pb.Collection(_collectionId).UpdateAsync(_recordId, new Dictionary<string, object?>
        {
            ["documents-"] = new[] { filename }
        });
        await LoadAsync();  // Reload
    }

    public async Task AddFilesAsync(List<FileAttachment> files)
    {
        var filesWithModifier = files.Select(f => new FileAttachment
        {
            FieldName = "documents+",
            FileName = f.FileName,
            Content = f.Content,
            ContentType = f.ContentType
        }).ToList();

        await _pb.Collection(_collectionId).UpdateAsync(_recordId, new Dictionary<string, object?>(), files: filesWithModifier);
        await LoadAsync();  // Reload
    }
}

// Usage
var manager = new FileManager(pb, "example", "RECORD_ID");
await manager.LoadAsync();
var files = manager.GetFiles();
foreach (var file in files)
{
    Console.WriteLine($"File: {file}");
}
```

### Example 4: Protected Document Viewer

```csharp
async Task<string?> ViewProtectedDocumentAsync(string recordId, string filename)
{
    // Authenticate if needed
    if (!pb.AuthStore.IsValid())
    {
        await pb.Collection("users").AuthWithPasswordAsync("user@example.com", "pass");
    }

    // Get token
    string? token;
    try
    {
        token = await pb.Files.GetTokenAsync();
    }
    catch (Exception err)
    {
        Console.Error.WriteLine($"Failed to get file token: {err}");
        return null;
    }

    // Get record and file URL
    var record = await pb.Collection("documents").GetOneAsync(recordId);
    var url = pb.Files.GetUrl(record, filename, token: token);

    // Return URL for use in browser or download
    return url;
}
```

### Example 5: Image Gallery with Thumbnails

```csharp
async Task DisplayImageGalleryAsync(string recordId)
{
    var record = await pb.Collection("gallery").GetOneAsync(recordId);
    var images = record["images"] as List<object?> ?? new List<object?>();

    foreach (var imageObj in images)
    {
        var filename = imageObj?.ToString();
        if (filename == null) continue;

        // Thumbnail for grid view
        var thumbUrl = pb.Files.GetUrl(record, filename, thumb: "200x200f");  // Fit inside 200x200

        // Full size for lightbox
        var fullUrl = pb.Files.GetUrl(record, filename, thumb: "1200x800f");  // Larger size

        Console.WriteLine($"Thumbnail: {thumbUrl}");
        Console.WriteLine($"Full size: {fullUrl}");
    }
}
```

## File Field Modifiers

### Summary

- **No modifier** - Replace all files: `documents: [file1, file2]`
- **`+` suffix** - Append files: `documents+: file3`
- **`+` prefix** - Prepend files: `+documents: file0`
- **`-` suffix** - Delete files: `documents-: ['file1.pdf']`

## Best Practices

1. **File Size Limits**: Always validate file sizes on the client before upload
2. **MIME Types**: Configure allowed MIME types in collection field settings
3. **Thumbnails**: Pre-generate common thumbnail sizes for better performance
4. **Protected Files**: Use protected files for sensitive documents (ID cards, contracts)
5. **Token Refresh**: Refresh file tokens before they expire for protected files
6. **Error Handling**: Handle 404 errors for missing files and 401 for protected file access
7. **Filename Sanitization**: Files are automatically sanitized, but validate on client side too

## Error Handling

```csharp
try
{
    var fileAttachment = new FileAttachment
    {
        FieldName = "documents",
        FileName = "test.txt",
        Content = Encoding.UTF8.GetBytes("content"),
        ContentType = "text/plain"
    };

    var record = await pb.Collection("example").CreateAsync(
        new Dictionary<string, object?> { ["title"] = "Test" },
        files: new[] { fileAttachment }
    );
}
catch (ClientResponseError err)
{
    if (err.Status == 413)
    {
        Console.Error.WriteLine("File too large");
    }
    else if (err.Status == 400)
    {
        Console.Error.WriteLine("Invalid file type or field validation failed");
    }
    else if (err.Status == 403)
    {
        Console.Error.WriteLine("Insufficient permissions");
    }
    else
    {
        Console.Error.WriteLine($"Upload failed: {err}");
    }
}
```

## Storage Options

By default, BosBase stores files in `pb_data/storage` on the local filesystem. For production, you can configure S3-compatible storage (AWS S3, MinIO, Wasabi, DigitalOcean Spaces, etc.) from:
**Dashboard > Settings > Files storage**

This is configured server-side and doesn't require SDK changes.

## Related Documentation

- [Collections](./COLLECTIONS.md) - Collection and field configuration
- [Authentication](./AUTHENTICATION.md) - Required for protected files

