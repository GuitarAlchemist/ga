namespace GaApi.Services;

using GA.Business.AI.HandPose;

/// <summary>
///     Timestamped guitar positions detected from a single video frame
/// </summary>
public record TimestampedGuitarPosition(double TimestampSeconds, GuitarPosition[] Positions);

/// <summary>
///     Processes extracted video frames through the hand pose inference pipeline
///     to produce timestamped guitar string/fret positions
/// </summary>
public class HandPosePipeline(HandPoseClient handPoseClient, ILogger<HandPosePipeline> logger)
{
    /// <summary>
    ///     Send each frame to the hand pose service and collect guitar positions with timestamps
    /// </summary>
    public async Task<IReadOnlyList<TimestampedGuitarPosition>> ProcessFramesAsync(
        IReadOnlyList<ExtractedFrame> frames,
        CancellationToken ct = default)
    {
        if (frames.Count == 0)
        {
            return [];
        }

        logger.LogInformation("Processing {FrameCount} frames through hand pose pipeline", frames.Count);

        var results = new List<TimestampedGuitarPosition>();

        for (var i = 0; i < frames.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var frame = frames[i];

            try
            {
                // Step 1: Detect hand pose from the frame image
                var handPoseResponse = await handPoseClient.InferAsync(frame.ImageData, $"frame_{frame.FrameIndex:D4}.jpg", ct);

                if (handPoseResponse.Hands.Count == 0)
                {
                    logger.LogDebug("No hands detected in frame {FrameIndex}", frame.FrameIndex);
                    continue;
                }

                // Step 2: Map hand pose to guitar string/fret positions
                var guitarMapping = await handPoseClient.MapToGuitarAsync(handPoseResponse, cancellationToken: ct);

                if (guitarMapping.Positions.Count == 0)
                {
                    logger.LogDebug("No guitar positions mapped in frame {FrameIndex}", frame.FrameIndex);
                    continue;
                }

                results.Add(new TimestampedGuitarPosition(
                    frame.TimestampSeconds,
                    [.. guitarMapping.Positions]));

                if ((i + 1) % 50 == 0)
                {
                    logger.LogInformation("Processed {Count}/{Total} frames, {Positions} positions found so far",
                        i + 1, frames.Count, results.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process frame {FrameIndex}, skipping", frame.FrameIndex);
            }
        }

        logger.LogInformation("Hand pose pipeline complete: {PositionCount} positions from {FrameCount} frames",
            results.Count, frames.Count);

        return results;
    }
}
