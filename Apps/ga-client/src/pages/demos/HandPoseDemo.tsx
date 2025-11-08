import { useState, useRef } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Container,
  Typography,
  Alert,
  Stack,
  Paper,
  Grid,
  Chip,
} from '@mui/material';
import {
  PanTool as PanToolIcon,
  CameraAlt as CameraIcon,
  Stop as StopIcon,
  Upload as UploadIcon,
} from '@mui/icons-material';

interface HandPoseResult {
  landmarks: Array<{ x: number; y: number; z: number }>;
  handedness: string;
  confidence: number;
}

const HandPoseDemo = () => {
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<HandPoseResult | null>(null);
  const [imagePreview, setImagePreview] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const [isStreaming, setIsStreaming] = useState(false);

  const API_BASE_URL = 'http://localhost:5232/api';

  const handleFileUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    setIsProcessing(true);
    setError(null);

    try {
      // Create preview
      const reader = new FileReader();
      reader.onload = (e) => {
        setImagePreview(e.target?.result as string);
      };
      reader.readAsDataURL(file);

      // Send to API
      const formData = new FormData();
      formData.append('image', file);

      const response = await fetch(`${API_BASE_URL}/guitar-playing/detect-hand-pose`, {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        throw new Error('Failed to detect hand pose');
      }

      const data = await response.json();
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred');
    } finally {
      setIsProcessing(false);
    }
  };

  const startWebcam = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true });
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        setIsStreaming(true);
      }
    } catch (err) {
      setError('Failed to access webcam. Please check permissions.');
    }
  };

  const stopWebcam = () => {
    if (videoRef.current && videoRef.current.srcObject) {
      const stream = videoRef.current.srcObject as MediaStream;
      stream.getTracks().forEach(track => track.stop());
      videoRef.current.srcObject = null;
      setIsStreaming(false);
    }
  };

  const captureFrame = async () => {
    if (!videoRef.current) return;

    const canvas = document.createElement('canvas');
    canvas.width = videoRef.current.videoWidth;
    canvas.height = videoRef.current.videoHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.drawImage(videoRef.current, 0, 0);
    
    canvas.toBlob(async (blob) => {
      if (!blob) return;

      setIsProcessing(true);
      setError(null);

      try {
        const formData = new FormData();
        formData.append('image', blob, 'webcam-capture.jpg');

        const response = await fetch(`${API_BASE_URL}/guitar-playing/detect-hand-pose`, {
          method: 'POST',
          body: formData,
        });

        if (!response.ok) {
          throw new Error('Failed to detect hand pose');
        }

        const data = await response.json();
        setResult(data);
        setImagePreview(canvas.toDataURL());
      } catch (err) {
        setError(err instanceof Error ? err.message : 'An error occurred');
      } finally {
        setIsProcessing(false);
      }
    }, 'image/jpeg');
  };

  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" sx={{ fontWeight: 700, mb: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
          <PanToolIcon fontSize="large" />
          Hand Pose Detection
        </Typography>
        <Typography variant="body1" color="text.secondary">
          AI-powered hand pose detection for guitar playing analysis. Upload an image or use your webcam
          to detect hand positions and finger landmarks.
        </Typography>
      </Box>

      <Grid container spacing={3}>
        {/* Input Section */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom sx={{ fontWeight: 600 }}>
                Input
              </Typography>

              <Stack spacing={2}>
                {/* File Upload */}
                <Box>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/*"
                    onChange={handleFileUpload}
                    style={{ display: 'none' }}
                  />
                  <Button
                    variant="contained"
                    startIcon={<UploadIcon />}
                    onClick={() => fileInputRef.current?.click()}
                    disabled={isProcessing}
                    fullWidth
                  >
                    Upload Image
                  </Button>
                </Box>

                {/* Webcam Controls */}
                <Box>
                  {!isStreaming ? (
                    <Button
                      variant="outlined"
                      startIcon={<CameraIcon />}
                      onClick={startWebcam}
                      fullWidth
                    >
                      Start Webcam
                    </Button>
                  ) : (
                    <Stack spacing={1}>
                      <Button
                        variant="contained"
                        startIcon={<CameraIcon />}
                        onClick={captureFrame}
                        disabled={isProcessing}
                        fullWidth
                      >
                        Capture Frame
                      </Button>
                      <Button
                        variant="outlined"
                        color="error"
                        startIcon={<StopIcon />}
                        onClick={stopWebcam}
                        fullWidth
                      >
                        Stop Webcam
                      </Button>
                    </Stack>
                  )}
                </Box>

                {/* Webcam Preview */}
                {isStreaming && (
                  <Paper sx={{ p: 2, bgcolor: 'background.default' }}>
                    <video
                      ref={videoRef}
                      autoPlay
                      playsInline
                      style={{ width: '100%', borderRadius: 8 }}
                    />
                  </Paper>
                )}

                {/* Image Preview */}
                {imagePreview && !isStreaming && (
                  <Paper sx={{ p: 2, bgcolor: 'background.default' }}>
                    <img
                      src={imagePreview}
                      alt="Preview"
                      style={{ width: '100%', borderRadius: 8 }}
                    />
                  </Paper>
                )}

                {/* Error Display */}
                {error && (
                  <Alert severity="error" onClose={() => setError(null)}>
                    {error}
                  </Alert>
                )}
              </Stack>
            </CardContent>
          </Card>
        </Grid>

        {/* Results Section */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom sx={{ fontWeight: 600 }}>
                Results
              </Typography>

              {result ? (
                <Stack spacing={2}>
                  <Box>
                    <Typography variant="subtitle2" color="text.secondary">
                      Handedness
                    </Typography>
                    <Chip
                      label={result.handedness}
                      color="primary"
                      sx={{ mt: 0.5 }}
                    />
                  </Box>

                  <Box>
                    <Typography variant="subtitle2" color="text.secondary">
                      Confidence
                    </Typography>
                    <Typography variant="h6">
                      {(result.confidence * 100).toFixed(1)}%
                    </Typography>
                  </Box>

                  <Box>
                    <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                      Landmarks Detected
                    </Typography>
                    <Typography variant="body2">
                      {result.landmarks.length} hand landmarks detected
                    </Typography>
                  </Box>

                  <Paper sx={{ p: 2, bgcolor: 'background.default', maxHeight: 300, overflow: 'auto' }}>
                    <Typography variant="caption" component="pre" sx={{ fontFamily: 'monospace' }}>
                      {JSON.stringify(result, null, 2)}
                    </Typography>
                  </Paper>
                </Stack>
              ) : (
                <Typography variant="body2" color="text.secondary">
                  Upload an image or capture from webcam to see hand pose detection results.
                </Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Info Section */}
      <Box sx={{ mt: 4, p: 3, bgcolor: 'background.paper', borderRadius: 2 }}>
        <Typography variant="h6" sx={{ fontWeight: 600, mb: 2 }}>
          About Hand Pose Detection
        </Typography>
        <Typography variant="body2" color="text.secondary" paragraph>
          This demo uses AI-powered computer vision to detect hand poses in images. It can identify
          21 hand landmarks including finger joints, palm position, and wrist location.
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Applications include guitar fingering analysis, gesture recognition, and interactive
          music learning experiences.
        </Typography>
      </Box>
    </Container>
  );
};

export default HandPoseDemo;

