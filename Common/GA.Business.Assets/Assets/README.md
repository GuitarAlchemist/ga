# Asset Management System

## Overview

The Asset Management System provides infrastructure for managing 3D models (GLB files) used in the BSP DOOM Explorer. It
supports importing, storing, categorizing, and retrieving 3D assets with metadata.

## Features

- **Asset Categories**: Architecture, AlchemyProps, Gems, Jars, Torches, Artifacts, Decorative
- **Metadata Tracking**: Name, category, poly count, license, source, author, tags
- **Bounding Box**: Automatic calculation of center, size, and volume
- **File Storage**: Hash-based IDs for deduplication
- **Search**: By category, tags, or ID

## Usage

### Import a GLB File

```csharp
var service = serviceProvider.GetRequiredService<IAssetLibraryService>();

var metadata = await service.ImportGlbAsync(
    path: "path/to/model.glb",
    metadata: new AssetMetadata
    {
        Id = "", // Will be auto-generated
        Name = "Ankh Symbol",
        Category = AssetCategory.AlchemyProps,
        License = "CC Attribution",
        Source = "https://sketchfab.com/...",
        Author = "Artist Name",
        Tags = new Dictionary<string, string>
        {
            ["symbol"] = "ankh",
            ["theme"] = "egyptian",
            ["material"] = "gold"
        }
    }
);

Console.WriteLine($"Imported asset {metadata.Id} with {metadata.PolyCount} polygons");
```

### Get Assets by Category

```csharp
var gems = await service.GetAssetsByCategoryAsync(AssetCategory.Gems);

foreach (var gem in gems)
{
    Console.WriteLine($"{gem.Name}: {gem.PolyCount} polygons");
}
```

### Search by Tags

```csharp
var egyptianAssets = await service.SearchByTagsAsync(new Dictionary<string, string>
{
    ["theme"] = "egyptian"
});
```

### Download GLB File

```csharp
var glbData = await service.DownloadGlbAsync(assetId);
await File.WriteAllBytesAsync("output.glb", glbData);
```

## Asset Categories

### Architecture

Pyramids, pillars, obelisks - structural elements for BSP rooms

### AlchemyProps

Ankh, Eye of Horus, flasks, scrolls - alchemy themed props

### Gems

Various gem cuts and precious stones - for alchemy ingredients

### Jars

Canopic jars, vessels, containers - for potions and storage

### Torches

Light sources - torches, braziers, wall-mounted lights

### Artifacts

Scarabs, statues, masks, sarcophagi - Egyptian artifacts

### Decorative

General decoration elements

## Metadata Fields

- **Id**: Unique identifier (SHA256 hash of file content)
- **Name**: Human-readable name
- **Category**: Asset category enum
- **GlbPath**: Path to GLB file (or GridFS reference)
- **PolyCount**: Number of polygons/triangles
- **License**: License information (e.g., "CC Attribution")
- **Source**: Source URL where asset was downloaded
- **Author**: Original creator/artist
- **Tags**: Dictionary of key-value pairs for searching
- **Bounds**: Axis-aligned bounding box (min/max coordinates)
- **FileSizeBytes**: File size in bytes
- **IsOptimized**: Whether optimized for WebGPU
- **ThumbnailPath**: Optional thumbnail image path

## Bounding Box

The `BoundingBox` type provides:

```csharp
var bounds = metadata.Bounds;

// Center point
var center = bounds.Center; // Vector3

// Size (width, height, depth)
var size = bounds.Size; // Vector3

// Volume
var volume = bounds.Volume; // float
```

## Storage

Assets are stored in:

- **File System**: `%APPDATA%/GuitarAlchemist/Assets/{id}.glb`
- **MongoDB**: Metadata in `assets` collection
- **GridFS**: Large GLB files (> 16MB)

## Future Enhancements

- [ ] Blender to GLB conversion
- [ ] GLB optimization (decimation, texture compression)
- [ ] Thumbnail generation
- [ ] LOD (Level of Detail) generation
- [ ] Material enhancement (emissive, reflective)
- [ ] Asset browser UI
- [ ] Batch import from directories

## See Also

- [Implementation Plan](../../../../docs/IMPLEMENTATION_PLAN_BLENDER_GROTHENDIECK.md)
- [3D Asset Links](../../../../docs/3D_ASSET_LINKS.md)
- [Implementation Status](../../../../docs/IMPLEMENTATION_STATUS.md)

