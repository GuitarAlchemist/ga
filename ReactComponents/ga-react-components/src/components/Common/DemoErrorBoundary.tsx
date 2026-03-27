/**
 * DemoErrorBoundary
 *
 * Shared error boundary for 3D demo pages. Catches render errors in Three.js /
 * Pixi.js canvases so a crash in one demo doesn't white-screen the whole app.
 */

import React, { Component } from 'react';
import { Box, Typography, Button, Paper } from '@mui/material';

interface Props {
  children: React.ReactNode;
  /** Optional demo name shown in the fallback UI */
  demoName?: string;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class DemoErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false, error: null };

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo) {
    console.error(
      `[DemoErrorBoundary${this.props.demoName ? ` — ${this.props.demoName}` : ''}]`,
      error,
      info.componentStack,
    );
  }

  handleRetry = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError) {
      return (
        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            width: '100%',
            height: '100%',
            minHeight: 300,
            bgcolor: '#1a1a1a',
          }}
        >
          <Paper
            sx={{
              p: 4,
              maxWidth: 480,
              textAlign: 'center',
              bgcolor: '#252525',
              color: '#ccc',
            }}
          >
            <Typography variant="h6" sx={{ mb: 1, color: '#ff6b6b' }}>
              {this.props.demoName ?? 'Demo'} failed to render
            </Typography>
            <Typography variant="body2" sx={{ mb: 2, color: '#999' }}>
              {this.state.error?.message ?? 'An unexpected error occurred.'}
            </Typography>
            <Button variant="outlined" size="small" onClick={this.handleRetry}>
              Retry
            </Button>
          </Paper>
        </Box>
      );
    }

    return this.props.children;
  }
}

export default DemoErrorBoundary;
