import React from 'react';
import { Box, Tooltip } from '@mui/material';
import StarIcon from '@mui/icons-material/Star';
import StarBorderIcon from '@mui/icons-material/StarBorder';

// A compact 1–5 star rating. `value` is clamped to [0,5] and rounded; the
// remaining pips render as outlines so the row always shows five stars.
// Accessible: the row carries an aria-label and a tooltip with the raw score.
export interface StarRatingProps {
  /** Star count 1..5 (the catalog's `stars` field). */
  value: number;
  /** Optional continuous score 0..1 (the catalog's `score01`) for the tooltip. */
  score01?: number;
  /** Pixel size of each star. */
  size?: number;
}

export const StarRating: React.FC<StarRatingProps> = ({ value, score01, size = 16 }) => {
  const filled = Math.max(0, Math.min(5, Math.round(value)));
  const label =
    score01 != null ? `${filled} of 5 stars (score ${score01.toFixed(2)})` : `${filled} of 5 stars`;
  return (
    <Tooltip title={label} placement="top" enterDelay={300}>
      <Box
        data-testid="star-rating"
        role="img"
        aria-label={label}
        sx={{ display: 'inline-flex', alignItems: 'center', lineHeight: 1, color: 'warning.main' }}
      >
        {Array.from({ length: 5 }, (_, i) =>
          i < filled ? (
            <StarIcon key={i} sx={{ fontSize: size }} />
          ) : (
            <StarBorderIcon key={i} sx={{ fontSize: size, color: 'text.disabled' }} />
          ),
        )}
      </Box>
    </Tooltip>
  );
};
