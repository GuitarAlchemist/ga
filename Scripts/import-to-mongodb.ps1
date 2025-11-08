# Import Guitar Alchemist Chord Data to Local MongoDB
# Usage: .\Scripts\import-to-mongodb.ps1

param(
    [string]$DataFile = "C:\Temp\GaExport\all-chords.json",
    [string]$Database = "guitar-alchemist",
    [string]$Collection = "chords",
    [string]$MongoHost = "localhost",
    [int]$MongoPort = 27017
)

Write-Host "🎸 Guitar Alchemist - MongoDB Import Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if MongoDB is running
Write-Host "Checking MongoDB connection..." -ForegroundColor Yellow
try
{
    $mongoTest = mongosh --quiet --eval "db.version()" 2>&1
    if ($LASTEXITCODE -eq 0)
    {
        Write-Host "✓ MongoDB is running (version: $mongoTest)" -ForegroundColor Green
    }
    else
    {
        Write-Host "✗ MongoDB is not running or not accessible" -ForegroundColor Red
        Write-Host "  Please start MongoDB and try again" -ForegroundColor Red
        exit 1
    }
}
catch
{
    Write-Host "✗ MongoDB is not running or mongosh is not installed" -ForegroundColor Red
    exit 1
}

# Check if data file exists
Write-Host "Checking data file..." -ForegroundColor Yellow
if (-not (Test-Path $DataFile))
{
    Write-Host "✗ Data file not found: $DataFile" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please export chord data first:" -ForegroundColor Yellow
    Write-Host "  dotnet run --project Apps/GaDataCLI/GaDataCLI.csproj -- -e chords -o C:\Temp\GaExport -q" -ForegroundColor White
    exit 1
}

$fileSize = (Get-Item $DataFile).Length / 1MB
Write-Host "✓ Data file found: $DataFile ($([math]::Round($fileSize, 2) ) MB)" -ForegroundColor Green

# Import data
Write-Host ""
Write-Host "Importing data to MongoDB..." -ForegroundColor Yellow
Write-Host "  Database: $Database" -ForegroundColor White
Write-Host "  Collection: $Collection" -ForegroundColor White
Write-Host ""

$importCmd = "mongoimport --db $Database --collection $Collection --file `"$DataFile`" --jsonArray"
Write-Host "Running: $importCmd" -ForegroundColor Gray
Write-Host ""

$startTime = Get-Date
Invoke-Expression $importCmd

if ($LASTEXITCODE -eq 0)
{
    $duration = (Get-Date) - $startTime
    Write-Host ""
    Write-Host "✓ Import completed successfully in $([math]::Round($duration.TotalSeconds, 2) ) seconds" -ForegroundColor Green
}
else
{
    Write-Host ""
    Write-Host "✗ Import failed" -ForegroundColor Red
    exit 1
}

# Verify import
Write-Host ""
Write-Host "Verifying import..." -ForegroundColor Yellow
$countCmd = "mongosh --quiet --eval `"use $Database; db.$Collection.countDocuments()`""
$count = Invoke-Expression $countCmd

Write-Host "✓ Document count: $count" -ForegroundColor Green

# Create indexes
Write-Host ""
Write-Host "Creating indexes..." -ForegroundColor Yellow

$indexScript = @"
use $Database;

print('Creating compound index on Quality, Extension, StackingType...');
db.$Collection.createIndex({ Quality: 1, Extension: 1, StackingType: 1 });

print('Creating index on PitchClassSet...');
db.$Collection.createIndex({ PitchClassSet: 1 });

print('Creating index on ParentScale and ScaleDegree...');
db.$Collection.createIndex({ ParentScale: 1, ScaleDegree: 1 });

print('Creating text index on Name and Description...');
db.$Collection.createIndex({ Name: 'text', Description: 'text' });

print('Indexes created successfully!');
db.$Collection.getIndexes();
"@

$indexScript | mongosh --quiet

Write-Host "✓ Indexes created" -ForegroundColor Green

# Show sample data
Write-Host ""
Write-Host "Sample chord data:" -ForegroundColor Yellow
$sampleCmd = "mongosh --quiet --eval `"use $Database; db.$Collection.findOne()`""
Invoke-Expression $sampleCmd

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "✓ Import Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database: $Database" -ForegroundColor White
Write-Host "Collection: $Collection" -ForegroundColor White
Write-Host "Documents: $count" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test queries in mongosh:" -ForegroundColor White
Write-Host "     mongosh" -ForegroundColor Gray
Write-Host "     use $Database" -ForegroundColor Gray
Write-Host "     db.$Collection.find({ Quality: 'Major' }).limit(5)" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Start the API:" -ForegroundColor White
Write-Host "     dotnet run --project Apps/ga-server/GaApi" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Test API endpoints:" -ForegroundColor White
Write-Host "     curl http://localhost:5000/api/chords/quality/Major?limit=5" -ForegroundColor Gray
Write-Host ""

