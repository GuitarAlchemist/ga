import React from 'react';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';
import IxHandVoicingLab from '../components/IxHandVoicingLab';

const IxHandVoicingTest: React.FC = () => (
  <DemoErrorBoundary demoName="IX Hand Voicing Lab">
    <IxHandVoicingLab />
  </DemoErrorBoundary>
);

export default IxHandVoicingTest;
