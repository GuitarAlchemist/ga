import React, { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Box,
  Stepper,
  Step,
  StepLabel,
  StepContent,
  Card,
  CardContent,
  Chip,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  IconButton,
  Tooltip
} from '@mui/material';
import {
  Help,
  Close,
  Search,
  Analytics,
  Timeline,
  Info,
  PlayArrow,
  CheckCircle,
  Lightbulb,
  Speed,
  Share
} from '@mui/icons-material';

interface BSPTutorialProps {
  onStartDemo?: (demoType: 'spatial' | 'tonal' | 'progression') => void;
}

const tutorialSteps = [
  {
    label: 'Welcome to BSP Analysis',
    icon: <Info />,
    content: {
      title: 'Binary Space Partitioning for Music',
      description: 'BSP organizes musical elements in a hierarchical tree structure, enabling fast similarity searches and harmonic analysis.',
      features: [
        'Sub-millisecond spatial queries',
        'Intelligent chord suggestions',
        'Harmonic relationship analysis',
        'Voice leading optimization'
      ],
      tip: 'BSP is particularly powerful for analyzing chord progressions and finding similar harmonic structures.'
    }
  },
  {
    label: 'Spatial Queries',
    icon: <Search />,
    content: {
      title: 'Find Similar Chords',
      description: 'Search for chords within a specified spatial radius using different partition strategies.',
      features: [
        'Circle of Fifths partitioning',
        'Chromatic distance calculation',
        'Set complexity analysis',
        'Tonal hierarchy organization'
      ],
      tip: 'Try different partition strategies to see how they affect the similarity results.'
    }
  },
  {
    label: 'Tonal Context Analysis',
    icon: <Analytics />,
    content: {
      title: 'Understand Harmonic Context',
      description: 'Analyze how well a chord fits within different tonal regions and get confidence scores.',
      features: [
        'Regional fit analysis',
        'Confidence scoring',
        'Common tone calculation',
        'Tonal center identification'
      ],
      tip: 'Higher confidence scores indicate better fit within the identified tonal region.'
    }
  },
  {
    label: 'Progression Analysis',
    icon: <Timeline />,
    content: {
      title: 'Analyze Chord Progressions',
      description: 'Examine harmonic relationships and transitions in chord progressions.',
      features: [
        'Transition smoothness analysis',
        'Common tone tracking',
        'Distance calculations',
        'Overall progression metrics'
      ],
      tip: 'Smoother transitions typically indicate better voice leading and harmonic flow.'
    }
  },
  {
    label: 'Performance & Export',
    icon: <Speed />,
    content: {
      title: 'Monitor and Share Results',
      description: 'Track query performance and export your analysis results for further use.',
      features: [
        'Real-time performance metrics',
        'Connection status monitoring',
        'Export to JSON, CSV, Markdown',
        'Shareable analysis URLs'
      ],
      tip: 'Use the export feature to document your musical analysis findings.'
    }
  }
];

export const BSPTutorial: React.FC<BSPTutorialProps> = ({ onStartDemo }) => {
  const [open, setOpen] = useState(false);
  const [activeStep, setActiveStep] = useState(0);

  const handleNext = () => {
    setActiveStep((prevActiveStep) => prevActiveStep + 1);
  };

  const handleBack = () => {
    setActiveStep((prevActiveStep) => prevActiveStep - 1);
  };

  const handleReset = () => {
    setActiveStep(0);
  };

  const handleStartDemo = (demoType: 'spatial' | 'tonal' | 'progression') => {
    setOpen(false);
    onStartDemo?.(demoType);
  };

  return (
    <>
      <Tooltip title="Interactive Tutorial">
        <IconButton onClick={() => setOpen(true)} size="small">
          <Help />
        </IconButton>
      </Tooltip>

      <Dialog open={open} onClose={() => setOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            BSP Interactive Tutorial
            <IconButton onClick={() => setOpen(false)} size="small">
              <Close />
            </IconButton>
          </Box>
        </DialogTitle>
        
        <DialogContent>
          <Stepper activeStep={activeStep} orientation="vertical">
            {tutorialSteps.map((step, index) => (
              <Step key={step.label}>
                <StepLabel
                  optional={
                    index === tutorialSteps.length - 1 ? (
                      <Typography variant="caption">Last step</Typography>
                    ) : null
                  }
                  icon={step.icon}
                >
                  {step.label}
                </StepLabel>
                <StepContent>
                  <Card variant="outlined" sx={{ mb: 2 }}>
                    <CardContent>
                      <Typography variant="h6" gutterBottom>
                        {step.content.title}
                      </Typography>
                      
                      <Typography variant="body2" paragraph>
                        {step.content.description}
                      </Typography>

                      <Typography variant="subtitle2" gutterBottom>
                        Key Features:
                      </Typography>
                      <List dense>
                        {step.content.features.map((feature, featureIndex) => (
                          <ListItem key={featureIndex}>
                            <ListItemIcon>
                              <CheckCircle color="success" fontSize="small" />
                            </ListItemIcon>
                            <ListItemText primary={feature} />
                          </ListItem>
                        ))}
                      </List>

                      <Box sx={{ display: 'flex', alignItems: 'center', mt: 2, p: 1, bgcolor: 'info.light', borderRadius: 1 }}>
                        <Lightbulb sx={{ mr: 1, color: 'info.dark' }} />
                        <Typography variant="body2" sx={{ color: 'info.dark' }}>
                          <strong>Tip:</strong> {step.content.tip}
                        </Typography>
                      </Box>

                      {/* Demo buttons for specific steps */}
                      {index === 1 && onStartDemo && (
                        <Box sx={{ mt: 2 }}>
                          <Button
                            variant="outlined"
                            size="small"
                            startIcon={<PlayArrow />}
                            onClick={() => handleStartDemo('spatial')}
                          >
                            Try Spatial Query Demo
                          </Button>
                        </Box>
                      )}

                      {index === 2 && onStartDemo && (
                        <Box sx={{ mt: 2 }}>
                          <Button
                            variant="outlined"
                            size="small"
                            startIcon={<PlayArrow />}
                            onClick={() => handleStartDemo('tonal')}
                          >
                            Try Tonal Analysis Demo
                          </Button>
                        </Box>
                      )}

                      {index === 3 && onStartDemo && (
                        <Box sx={{ mt: 2 }}>
                          <Button
                            variant="outlined"
                            size="small"
                            startIcon={<PlayArrow />}
                            onClick={() => handleStartDemo('progression')}
                          >
                            Try Progression Demo
                          </Button>
                        </Box>
                      )}
                    </CardContent>
                  </Card>

                  <Box sx={{ mb: 2 }}>
                    <div>
                      <Button
                        variant="contained"
                        onClick={handleNext}
                        sx={{ mt: 1, mr: 1 }}
                        disabled={index === tutorialSteps.length - 1}
                      >
                        {index === tutorialSteps.length - 1 ? 'Finish' : 'Continue'}
                      </Button>
                      <Button
                        disabled={index === 0}
                        onClick={handleBack}
                        sx={{ mt: 1, mr: 1 }}
                      >
                        Back
                      </Button>
                    </div>
                  </Box>
                </StepContent>
              </Step>
            ))}
          </Stepper>

          {activeStep === tutorialSteps.length && (
            <Card sx={{ mt: 2, p: 2 }}>
              <Typography variant="h6" gutterBottom>
                🎉 Tutorial Complete!
              </Typography>
              <Typography variant="body2" paragraph>
                You're now ready to explore the full power of BSP musical analysis. 
                Start by trying different queries and see how the system responds.
              </Typography>
              
              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }}>
                <Chip label="Fast Queries" color="success" size="small" />
                <Chip label="Visual Feedback" color="primary" size="small" />
                <Chip label="Export Results" color="secondary" size="small" />
                <Chip label="Share Analysis" color="info" size="small" />
              </Box>

              <Typography variant="body2" color="text.secondary">
                Remember: The BSP system requires a connection to the backend API. 
                Check the connection status indicator in the top-right corner.
              </Typography>

              <Box sx={{ mt: 2 }}>
                <Button onClick={handleReset} sx={{ mr: 1 }}>
                  Restart Tutorial
                </Button>
                <Button variant="contained" onClick={() => setOpen(false)}>
                  Start Analyzing
                </Button>
              </Box>
            </Card>
          )}
        </DialogContent>
        
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </>
  );
};
