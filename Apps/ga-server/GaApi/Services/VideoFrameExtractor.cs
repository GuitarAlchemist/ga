namespace GaApi.Services;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Path = System.IO.Path;

/// <summary>
///     Extracted video frame with image data and timestamp
/// </summary>
public record ExtractedFrame(byte[] ImageData, double TimestampSeconds, int FrameIndex);

/// <summary>
///     Extracts video frames from YouTube URLs using yt-dlp and ffmpeg subprocesses
/// </summary>
public class VideoFrameExtractor(ILogger<VideoFrameExtractor> logger)
{
    /// <summary>
    ///     Download a YouTube video and extract frames at the specified FPS
    /// </summary>
    public async Task<IReadOnlyList<ExtractedFrame>> ExtractFramesAsync(
        string youtubeUrl,
        double fps = 2.0,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeUrl);

        var videoId = ExtractVideoId(youtubeUrl);
        if (string.IsNullOrEmpty(videoId))
        {
            throw new ArgumentException("Invalid YouTube URL", nameof(youtubeUrl));
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"ga-frames-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var videoPath = Path.Combine(tempDir, "video.mp4");

            // Step 1: Download video with yt-dlp
            logger.LogInformation("Downloading YouTube video {VideoId} to {TempDir}", videoId, tempDir);
            await RunProcessAsync(
                "yt-dlp",
                $"-f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/mp4\" -o \"{videoPath}\" \"{youtubeUrl}\"",
                ct);

            if (!File.Exists(videoPath))
            {
                throw new InvalidOperationException($"yt-dlp did not produce output file for video {videoId}");
            }

            logger.LogInformation("Video downloaded successfully: {VideoPath}", videoPath);

            // Step 2: Extract frames with ffmpeg
            var framePattern = Path.Combine(tempDir, "frame_%04d.jpg");
            logger.LogInformation("Extracting frames at {Fps} FPS", fps);
            await RunProcessAsync(
                "ffmpeg",
                $"-i \"{videoPath}\" -vf fps={fps:F1} -q:v 2 \"{framePattern}\"",
                ct);

            // Step 3: Read frames from disk
            var frameFiles = Directory.GetFiles(tempDir, "frame_*.jpg")
                .OrderBy(f => f)
                .ToList();

            logger.LogInformation("Extracted {FrameCount} frames", frameFiles.Count);

            var frames = new List<ExtractedFrame>(frameFiles.Count);
            for (var i = 0; i < frameFiles.Count; i++)
            {
                var imageData = await File.ReadAllBytesAsync(frameFiles[i], ct);
                var timestamp = i / fps;
                frames.Add(new ExtractedFrame(imageData, timestamp, i));
            }

            return frames;
        }
        finally
        {
            // Clean up temp files
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                    logger.LogDebug("Cleaned up temp directory {TempDir}", tempDir);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clean up temp directory {TempDir}", tempDir);
            }
        }
    }

    private async Task RunProcessAsync(string fileName, string arguments, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            logger.LogWarning(ex, "{Tool} is not installed or not on PATH", fileName);
            throw new InvalidOperationException(
                $"{fileName} is not installed or not on PATH. Please install it to use the YouTube-to-tab pipeline.", ex);
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            logger.LogError("{Tool} exited with code {ExitCode}. stderr: {StdErr}", fileName, process.ExitCode, stderr);
            throw new InvalidOperationException($"{fileName} failed with exit code {process.ExitCode}: {stderr}");
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            logger.LogDebug("{Tool} stderr: {StdErr}", fileName, stderr);
        }
    }

    private static string? ExtractVideoId(string url)
    {
        string[] patterns =
        [
            @"(?:youtube\.com\/watch\?v=|youtu\.be\/)([a-zA-Z0-9_-]{11})",
            @"youtube\.com\/embed\/([a-zA-Z0-9_-]{11})",
            @"youtube\.com\/v\/([a-zA-Z0-9_-]{11})"
        ];

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(url, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        if (Regex.IsMatch(url, @"^[a-zA-Z0-9_-]{11}$"))
        {
            return url;
        }

        return null;
    }
}
