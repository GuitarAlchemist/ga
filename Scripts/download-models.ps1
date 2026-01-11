$ErrorActionPreference = "Stop"

$modelsDir = "c:\Users\spare\source\repos\ga\GaCLI\models"
if (!(Test-Path $modelsDir)) { New-Item -ItemType Directory -Path $modelsDir -Force }

# Xenova usually maintains quantized ONNX models
$files = @(
    @("https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx", "all-MiniLM-L6-v2.onnx"),
    @("https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/vocab.txt", "vocab.txt")
)

foreach ($file in $files) {
    $url = $file[0]
    $name = $file[1]
    $dest = Join-Path $modelsDir $name
    
    # Always delete if too small (error message)
    if (Test-Path $dest) {
        $fileObj = Get-Item $dest
        if ($fileObj.Length -lt 1000) {
            Remove-Item $dest
        }
    }
    
    if (!(Test-Path $dest)) {
        Write-Host "Downloading $name from $url ..."
        curl -L $url -o $dest
        Write-Host "Downloaded $name"
    }
    else {
        Write-Host "$name already exists"
    }
}

# Copy to bin directory to be safe
$binDirs = Get-ChildItem "c:\Users\spare\source\repos\ga\GaCLI\bin\Debug" -Directory
foreach ($dir in $binDirs) {
    $targetDir = Join-Path $dir.FullName "models"
    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force }
    Copy-Item "$modelsDir\*" $targetDir -Force
    Write-Host "Copied models to $targetDir"
}
