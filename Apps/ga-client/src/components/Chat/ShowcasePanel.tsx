import React, { useEffect, useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Chip,
  Box,
  IconButton,
  Tooltip,
  Stack,
  CircularProgress,
  Alert,
} from '@mui/material';
import {
  Close,
  MusicNote,
  QueueMusic,
  Timeline,
  Code,
  AutoAwesome,
} from '@mui/icons-material';
import {
  fetchChatDemo,
  type ChatbotDemoCategory,
  type ChatbotDemoScript,
} from '../../services/chatService';

const ICON_MAP: Record<string, React.ReactNode> = {
  music_note: <MusicNote />,
  queue_music: <QueueMusic />,
  timeline: <Timeline />,
  code: <Code />,
  guitar: <AutoAwesome />,
};

const resolveIcon = (iconKey: string): React.ReactNode =>
  ICON_MAP[iconKey] ?? <AutoAwesome />;

export interface ShowcasePanelProps {
  open: boolean;
  apiBaseUrl: string;
  onClose: () => void;
  onSelectPrompt: (prompt: string) => void;
}

const ShowcasePanel: React.FC<ShowcasePanelProps> = ({
  open,
  apiBaseUrl,
  onClose,
  onSelectPrompt,
}) => {
  const [script, setScript] = useState<ChatbotDemoScript | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) return;

    const controller = new AbortController();
    setLoading(true);
    setError(null);

    fetchChatDemo(apiBaseUrl, controller.signal)
      .then((next) => {
        setScript(next);
        setLoading(false);
      })
      .catch((err) => {
        if (controller.signal.aborted) return;
        console.error('Failed to load showcase demo script:', err);
        setError('Could not load showcase. Check GaApi is reachable.');
        setLoading(false);
      });

    return () => controller.abort();
  }, [open, apiBaseUrl]);

  const handlePromptClick = (prompt: string) => {
    onSelectPrompt(prompt);
    onClose();
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="md"
      fullWidth
      data-testid="showcase-panel"
    >
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <AutoAwesome />
          <span>Showcase — What This Chatbot Can Do</span>
        </Box>
        <Tooltip title="Close">
          <IconButton aria-label="Close showcase" onClick={onClose} size="small">
            <Close />
          </IconButton>
        </Tooltip>
      </DialogTitle>

      <DialogContent dividers>
        {loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <CircularProgress size={28} />
          </Box>
        )}

        {error && !loading && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {script && !loading && (
          <Stack spacing={3}>
            {script.categories.map((category) => (
              <CategorySection
                key={category.id}
                category={category}
                onPromptClick={handlePromptClick}
              />
            ))}
          </Stack>
        )}

        {script && !loading && script.categories.length === 0 && (
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', py: 4 }}>
            No showcase categories available.
          </Typography>
        )}
      </DialogContent>

      <DialogActions>
        <Typography variant="caption" color="text.secondary" sx={{ mr: 'auto', pl: 1 }}>
          Click any prompt to send it. The chat will run the real backend pipeline.
        </Typography>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
};

interface CategorySectionProps {
  category: ChatbotDemoCategory;
  onPromptClick: (prompt: string) => void;
}

const CategorySection: React.FC<CategorySectionProps> = ({ category, onPromptClick }) => (
  <Box data-testid={`showcase-category-${category.id}`}>
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
      {resolveIcon(category.icon)}
      <Typography variant="h6" component="h2">
        {category.name}
      </Typography>
    </Box>
    <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
      {category.description}
    </Typography>
    <Stack spacing={1}>
      {category.prompts.map((entry) => (
        <Box
          key={entry.prompt}
          sx={{
            display: 'flex',
            flexDirection: { xs: 'column', sm: 'row' },
            alignItems: { xs: 'flex-start', sm: 'center' },
            gap: 1,
          }}
        >
          <Chip
            label={entry.prompt}
            color="primary"
            variant="outlined"
            onClick={() => onPromptClick(entry.prompt)}
            clickable
            sx={{ maxWidth: '100%', whiteSpace: 'normal', height: 'auto', py: 0.5 }}
          />
          <Typography variant="caption" color="text.secondary">
            {entry.description}
          </Typography>
        </Box>
      ))}
    </Stack>
  </Box>
);

export default ShowcasePanel;
