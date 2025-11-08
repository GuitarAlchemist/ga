import React, { ReactNode } from 'react';
import { Typography } from '@mui/material';

interface SectionHeaderProps {
  icon: ReactNode;
  text: string;
}

const SectionHeader: React.FC<SectionHeaderProps> = ({ icon, text }) => (
  <div className="metrics-dashboard__section-header">
    {icon}
    <Typography variant="h6">{text}</Typography>
  </div>
);

export default SectionHeader;
