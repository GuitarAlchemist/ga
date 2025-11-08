import { useState } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  TextField,
  Typography,
  Alert,
  Stack,
  Divider,
  Paper,
} from '@mui/material';
import {
  MusicNote as MusicNoteIcon,
  PlayArrow as PlayIcon,
  Stop as StopIcon,
  Download as DownloadIcon,
  Refresh as RefreshIcon,
} from '@mui/icons-material';

interface AudioSample {
  prompt: string;
  duration: number;
  audioUrl: string;
  generatedAt: string;
  cached: boolean;
}

const AVAILABLE_MODELS = [
  { id: 'facebook/musicgen-small', name: 'MusicGen Small', description: 'Fast, good quality' },
  { id: 'facebook/musicgen-large', name: 'MusicGen Large', description: 'Slower, best quality' },
  { id: 'stabilityai/stable-audio-open-1.0', name: 'Stable Audio', description: 'Open source audio generation' },
  { id: 'riffusion/riffusion-model-v1', name: 'Riffusion', description: 'Music generation from text' },
];

const EXAMPLE_PROMPTS = [
  'upbeat blues guitar riff in A minor',
  'calm acoustic guitar melody',
  'energetic rock guitar solo',
  'jazz guitar improvisation',
  'flamenco guitar strumming pattern',
  'ambient guitar soundscape',
];

const MusicGenerationDemo = () => {
  const [selectedModel, setSelectedModel] = useState(AVAILABLE_MODELS[0].id);
  const [prompt, setPrompt] = useState('');
  const [duration, setDuration] = useState(5);
  const [isGenerating, setIsGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [generatedSamples, setGeneratedSamples] = useState<AudioSample[]>([]);
  const [currentlyPlaying, setCurrentlyPlaying] = useState<string | null>(null);
  const [audioElements] = useState<Map<string, HTMLAudioElement>>(new Map());

  const API_BASE_URL = 'http://localhost:5232/api';

  const handleGenerate = async () => {
    if (!prompt.trim()) {
      setError('Please enter a prompt');
      return;
    }

    setIsGenerating(true);
    setError(null);

    try {
      const response = await fetch(`${API_BASE_URL}/musicgeneration/generate`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          description: prompt.trim(),
          modelId: selectedModel,
          duration,
        }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        let errorMessage = 'Failed to generate music';
        try {
          const errorData = JSON.parse(errorText);
          errorMessage = errorData.error || errorData.message || errorMessage;
        } catch {
          errorMessage = errorText || errorMessage;
        }
        throw new Error(errorMessage);
      }

      // Get the audio blob directly from the response
      const audioBlob = await response.blob();
      const audioUrl = URL.createObjectURL(audioBlob);

      const newSample: AudioSample = {
        prompt,
        duration,
        audioUrl,
        generatedAt: new Date().toISOString(),
        cached: false, // We don't have cache info from file response
      };

      setGeneratedSamples(prev => [newSample, ...prev]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'An error occurred');
    } finally {
      setIsGenerating(false);
    }
  };

  const handlePlay = async (audioUrl: string) => {
    // Stop currently playing audio
    if (currentlyPlaying && audioElements.has(currentlyPlaying)) {
      const currentAudio = audioElements.get(currentlyPlaying)!;
      currentAudio.pause();
      currentAudio.currentTime = 0;
    }

    if (currentlyPlaying === audioUrl) {
      setCurrentlyPlaying(null);
      return;
    }

    // Create or get audio element
    let audio = audioElements.get(audioUrl);
    if (!audio) {
      audio = new Audio(audioUrl);
      audio.addEventListener('ended', () => setCurrentlyPlaying(null));
      audio.addEventListener('error', (e) => {
        console.error('Audio playback error:', e);
        setError('Failed to play audio. The file may be corrupted or in an unsupported format.');
        setCurrentlyPlaying(null);
      });
      audioElements.set(audioUrl, audio);
    }

    try {
      await audio.play();
      setCurrentlyPlaying(audioUrl);
    } catch (err) {
      console.error('Play error:', err);
      setError('Failed to play audio. Please try again.');
      setCurrentlyPlaying(null);
    }
  };

  const handleDownload = (audioUrl: string, prompt: string) => {
    const link = document.createElement('a');
    link.href = audioUrl;
    link.download = `${prompt.substring(0, 30).replace(/[^a-z0-9]/gi, '-')}.wav`;
    link.click();
  };

  const handleUseExample = (examplePrompt: string) => {
    setPrompt(examplePrompt);
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700, display: 'flex', alignItems: 'center', gap: 1 }}>
        <MusicNoteIcon fontSize="large" />
        AI Music Generation
      </Typography>
      <Typography variant="body1" color="text.secondary" paragraph>
        Generate guitar music using Hugging Face AI models. Enter a text description and let AI create audio for you.
      </Typography>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Stack spacing={3}>
            {/* Model Selection */}
            <FormControl fullWidth>
              <InputLabel>AI Model</InputLabel>
              <Select
                value={selectedModel}
                label="AI Model"
                onChange={(e) => setSelectedModel(e.target.value)}
              >
                {AVAILABLE_MODELS.map((model) => (
                  <MenuItem key={model.id} value={model.id}>
                    <Box>
                      <Typography variant="body1">{model.name}</Typography>
                      <Typography variant="caption" color="text.secondary">
                        {model.description}
                      </Typography>
                    </Box>
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            {/* Prompt Input */}
            <TextField
              fullWidth
              label="Music Description"
              placeholder="Describe the music you want to generate..."
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              multiline
              rows={3}
              helperText="Be specific: include genre, mood, key, tempo, instruments, etc."
            />

            {/* Example Prompts */}
            <Box>
              <Typography variant="caption" color="text.secondary" gutterBottom display="block">
                Example prompts:
              </Typography>
              <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                {EXAMPLE_PROMPTS.map((example) => (
                  <Chip
                    key={example}
                    label={example}
                    size="small"
                    onClick={() => handleUseExample(example)}
                    sx={{ cursor: 'pointer' }}
                  />
                ))}
              </Stack>
            </Box>

            {/* Duration */}
            <TextField
              type="number"
              label="Duration (seconds)"
              value={duration}
              onChange={(e) => setDuration(Math.max(1, Math.min(30, parseInt(e.target.value) || 5)))}
              inputProps={{ min: 1, max: 30 }}
              helperText="1-30 seconds"
              sx={{ maxWidth: 200 }}
            />

            {/* Generate Button */}
            <Button
              variant="contained"
              size="large"
              onClick={handleGenerate}
              disabled={isGenerating || !prompt.trim()}
              startIcon={isGenerating ? <CircularProgress size={20} /> : <MusicNoteIcon />}
            >
              {isGenerating ? 'Generating...' : 'Generate Music'}
            </Button>

            {/* Error Display */}
            {error && (
              <Alert severity="error" onClose={() => setError(null)}>
                {error}
              </Alert>
            )}
          </Stack>
        </CardContent>
      </Card>

      {/* Generated Samples */}
      {generatedSamples.length > 0 && (
        <Box>
          <Typography variant="h5" gutterBottom sx={{ fontWeight: 600 }}>
            Generated Samples ({generatedSamples.length})
          </Typography>
          <Stack spacing={2}>
            {generatedSamples.map((sample, index) => (
              <Paper key={index} sx={{ p: 2 }}>
                <Stack spacing={2}>
                  <Box display="flex" justifyContent="space-between" alignItems="flex-start">
                    <Box flex={1}>
                      <Typography variant="body1" fontWeight={600}>
                        {sample.prompt}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {new Date(sample.generatedAt).toLocaleString()} • {sample.duration}s
                        {sample.cached && ' • Cached'}
                      </Typography>
                    </Box>
                    {sample.cached && (
                      <Chip label="Cached" size="small" color="success" />
                    )}
                  </Box>

                  <Divider />

                  <Stack direction="row" spacing={1}>
                    <Button
                      variant={currentlyPlaying === sample.audioUrl ? 'contained' : 'outlined'}
                      startIcon={currentlyPlaying === sample.audioUrl ? <StopIcon /> : <PlayIcon />}
                      onClick={() => handlePlay(sample.audioUrl)}
                    >
                      {currentlyPlaying === sample.audioUrl ? 'Stop' : 'Play'}
                    </Button>
                    <Button
                      variant="outlined"
                      startIcon={<DownloadIcon />}
                      onClick={() => handleDownload(sample.audioUrl, sample.prompt)}
                    >
                      Download
                    </Button>
                    <Button
                      variant="outlined"
                      startIcon={<RefreshIcon />}
                      onClick={() => {
                        setPrompt(sample.prompt);
                        setDuration(sample.duration);
                      }}
                    >
                      Regenerate
                    </Button>
                  </Stack>
                </Stack>
              </Paper>
            ))}
          </Stack>
        </Box>
      )}
    </Box>
  );
};

export default MusicGenerationDemo;

