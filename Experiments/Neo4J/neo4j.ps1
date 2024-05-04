param(
    [string]$Command
)

function Start-Neo4jBrowser {
    param (
        [string]$containerName = "neo4j-container",
        [string]$url = "http://localhost:7474"
    )

    # Check if the specified Docker container is running
    $container = docker ps --filter "name=$containerName" --format "{{.Names}}"

    if ($container -eq $containerName) {
        # If the container is running, open the URL in Chrome
        Write-Host "$containerName is running. Opening in Chrome..."
        Start-Process "chrome.exe" $url
    } else {
        # If the container is not running, output a message
        Write-Host "$containerName is not running. Please start the container first."
    }
}

switch ($Command) {
    "start" {
        Write-Host "Starting Neo4j container..."
        docker-compose up -d
        Write-Host "Neo4j container has been started."
		
		Start-Neo4jBrowser
    }
    "stop" {
        Write-Host "Stopping Neo4j container..."
        docker-compose down
        Write-Host "Neo4j container has been stopped."
    }
    "status" {
        Write-Host "Checking status of the Neo4j container..."
        docker-compose ps
    }
    default {
        Write-Host "Invalid command. Please use 'start', 'stop', or 'status'."
    }
}
