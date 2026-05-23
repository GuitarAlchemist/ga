/**
 * Modal Meadow — mobile fallback (v1.4).
 *
 * The desktop demo at `/test/modal-meadow` is a first-person, keyboard +
 * mouse + pointer-lock experience driving a 240k-instance grass scene
 * with PCF-soft shadows, UnrealBloomPass, and god rays. None of those
 * primitives port cleanly to mobile:
 *
 *   1. Pointer-lock API is desktop-only (no spec on iOS/Android Safari).
 *   2. WASD/keyboard movement assumes a physical keyboard.
 *   3. The grass instance count alone tanks integrated mobile GPUs;
 *      even at perf=low the post-pipeline still costs real ms/frame.
 *
 * Until full mobile support lands (touch joystick + perf=low + reduced
 * blade count — tracked in a separate plan), the polite shape is to NOT
 * mount the Three.js scene on small viewports and instead show a clean
 * "use a desktop" card with a screenshot preview and a back-link.
 *
 * This is purely additive: the existing desktop component is unchanged,
 * and the branch happens at the test-page level (`ModalMeadowTest.tsx`)
 * via `useIsMobile()`.
 */

import React from 'react';
import { Box, Paper, Typography, Button } from '@mui/material';
import { Link } from 'react-router-dom';

interface MobileFallbackProps {
  /** Optional screenshot URL to show as preview. */
  previewImage?: string;
}

export const MobileFallback: React.FC<MobileFallbackProps> = ({
  previewImage,
}) => {
  return (
    <Box
      sx={{
        height: '100vh',
        width: '100%',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        background:
          'linear-gradient(180deg, #1a2a18 0%, #2a3a1a 45%, #d9a86a 100%)',
        color: '#cdeac0',
        fontFamily: 'monospace',
        px: 3,
        py: 4,
        textAlign: 'center',
        overflowY: 'auto',
      }}
    >
      <Paper
        elevation={6}
        sx={{
          maxWidth: 540,
          width: '100%',
          backgroundColor: 'rgba(8, 14, 8, 0.85)',
          color: '#cdeac0',
          borderLeft: '3px solid #9be38a',
          px: { xs: 2.5, sm: 4 },
          py: { xs: 3, sm: 4 },
        }}
      >
        <Typography
          variant="caption"
          sx={{ color: '#7da876', letterSpacing: 2, display: 'block', mb: 1 }}
        >
          MODAL MEADOW
        </Typography>
        <Typography
          variant="h5"
          sx={{
            color: '#9be38a',
            fontFamily: 'monospace',
            mb: 2,
            fontWeight: 'normal',
          }}
        >
          Desktop only — for now
        </Typography>
        <Typography
          variant="body1"
          sx={{ color: '#cdeac0', mb: 2, lineHeight: 1.6 }}
        >
          This demo is a first-person walk through seven musical mode-regions.
          It needs a physical keyboard for WASD movement and a mouse for
          pointer-lock camera control — neither of which mobile browsers
          expose.
        </Typography>
        <Typography
          variant="body2"
          sx={{ color: '#7da876', mb: 3, lineHeight: 1.5 }}
        >
          Open this page on a laptop or desktop to walk from Lydian through
          Locrian and hear the modes shift around you.
        </Typography>

        {previewImage && (
          <Box
            component="img"
            src={previewImage}
            alt="Modal Meadow desktop preview"
            sx={{
              width: '100%',
              maxWidth: 480,
              height: 'auto',
              borderRadius: 1,
              border: '1px solid rgba(155, 227, 138, 0.3)',
              mb: 3,
              display: 'block',
              marginLeft: 'auto',
              marginRight: 'auto',
            }}
          />
        )}

        <Button
          component={Link}
          to="/test"
          variant="outlined"
          sx={{
            color: '#9be38a',
            borderColor: '#9be38a',
            fontFamily: 'monospace',
            '&:hover': {
              borderColor: '#cdeac0',
              backgroundColor: 'rgba(155, 227, 138, 0.1)',
            },
          }}
        >
          ← Back to demos
        </Button>
      </Paper>

      <Typography
        variant="caption"
        sx={{
          color: 'rgba(205, 234, 192, 0.5)',
          mt: 3,
          letterSpacing: 1,
          fontSize: 10,
        }}
      >
        Touch + reduced-detail mobile build is planned. Track at GitHub.
      </Typography>
    </Box>
  );
};

export default MobileFallback;
