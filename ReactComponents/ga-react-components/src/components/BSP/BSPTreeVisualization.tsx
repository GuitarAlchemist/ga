import React, { useRef, useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  FormControlLabel,
  Switch,
  Chip,
  Tooltip
} from '@mui/material';
import { BSPTreeInfoResponse } from './BSPApiService';

interface BSPTreeVisualizationProps {
  treeInfo?: BSPTreeInfoResponse | null;
}

interface TreeNode {
  id: string;
  name: string;
  type: 'root' | 'major' | 'minor';
  x: number;
  y: number;
  children: TreeNode[];
  parent?: TreeNode;
}

export const BSPTreeVisualization: React.FC<BSPTreeVisualizationProps> = ({
  treeInfo
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [showLabels, setShowLabels] = useState(true);
  const [highlightedNode, setHighlightedNode] = useState<string | null>(null);

  // Create a simplified BSP tree structure for visualization
  const createTreeStructure = (): TreeNode => {
    const root: TreeNode = {
      id: 'root',
      name: 'Chromatic Space',
      type: 'root',
      x: 0,
      y: 0,
      children: []
    };

    const majorRegion: TreeNode = {
      id: 'major',
      name: 'Major Regions',
      type: 'major',
      x: 0,
      y: 0,
      children: [],
      parent: root
    };

    const minorRegion: TreeNode = {
      id: 'minor',
      name: 'Minor Regions',
      type: 'minor',
      x: 0,
      y: 0,
      children: [],
      parent: root
    };

    root.children = [majorRegion, minorRegion];

    return root;
  };

  const calculateNodePositions = (node: TreeNode, x: number, y: number, level: number, siblingIndex: number, totalSiblings: number) => {
    const levelHeight = 100;
    const nodeSpacing = 200;
    
    // Calculate position based on level and sibling index
    if (level === 0) {
      // Root node at center
      node.x = x;
      node.y = y;
    } else {
      // Position children in a spread pattern
      const startX = x - ((totalSiblings - 1) * nodeSpacing) / 2;
      node.x = startX + siblingIndex * nodeSpacing;
      node.y = y + levelHeight;
    }

    // Recursively position children
    node.children.forEach((child, index) => {
      calculateNodePositions(child, node.x, node.y, level + 1, index, node.children.length);
    });
  };

  const drawTree = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const width = canvas.width;
    const height = canvas.height;
    const centerX = width / 2;
    const centerY = 80;

    // Clear canvas
    ctx.clearRect(0, 0, width, height);

    // Create and position tree
    const tree = createTreeStructure();
    calculateNodePositions(tree, centerX, centerY, 0, 0, 1);

    // Draw connections first
    const drawConnections = (node: TreeNode) => {
      node.children.forEach(child => {
        ctx.strokeStyle = '#e0e0e0';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(node.x, node.y + 25); // Start from bottom of parent node
        ctx.lineTo(child.x, child.y - 25); // End at top of child node
        ctx.stroke();
        
        drawConnections(child);
      });
    };

    drawConnections(tree);

    // Draw nodes
    const drawNode = (node: TreeNode) => {
      const isHighlighted = highlightedNode === node.id;
      const radius = 25;
      
      // Node colors based on type
      let fillColor = '#f5f5f5';
      let borderColor = '#ccc';
      
      switch (node.type) {
        case 'root':
          fillColor = '#2196F3';
          borderColor = '#1976D2';
          break;
        case 'major':
          fillColor = '#4CAF50';
          borderColor = '#388E3C';
          break;
        case 'minor':
          fillColor = '#FF9800';
          borderColor = '#F57C00';
          break;
      }

      if (isHighlighted) {
        fillColor = '#FFD700';
        borderColor = '#FFA000';
      }

      // Draw node circle
      ctx.fillStyle = fillColor;
      ctx.strokeStyle = borderColor;
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.arc(node.x, node.y, radius, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();

      // Draw node label
      if (showLabels) {
        ctx.fillStyle = node.type === 'root' ? '#fff' : '#333';
        ctx.font = '12px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(node.name, node.x, node.y + 4);
        
        // Draw additional info below node
        ctx.fillStyle = '#666';
        ctx.font = '10px Arial';
        ctx.fillText(
          node.type === 'root' ? 'Root' : `${node.type.charAt(0).toUpperCase() + node.type.slice(1)} Region`,
          node.x,
          node.y + radius + 15
        );
      }

      // Draw children
      node.children.forEach(child => drawNode(child));
    };

    drawNode(tree);

    // Draw legend
    ctx.fillStyle = '#333';
    ctx.textAlign = 'left';
    ctx.font = '12px Arial';
    ctx.fillText('BSP Tree Structure:', 10, 20);
    
    // Root node legend
    ctx.fillStyle = '#2196F3';
    ctx.beginPath();
    ctx.arc(20, 35, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#333';
    ctx.fillText('Chromatic Space (Root)', 35, 40);
    
    // Major region legend
    ctx.fillStyle = '#4CAF50';
    ctx.beginPath();
    ctx.arc(20, 55, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#333';
    ctx.fillText('Major Regions', 35, 60);
    
    // Minor region legend
    ctx.fillStyle = '#FF9800';
    ctx.beginPath();
    ctx.arc(20, 75, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillStyle = '#333';
    ctx.fillText('Minor Regions', 35, 80);

    // Draw tree info if available
    if (treeInfo) {
      ctx.fillStyle = '#666';
      ctx.font = '11px Arial';
      ctx.textAlign = 'right';
      ctx.fillText(`Total Regions: ${treeInfo.totalRegions}`, width - 10, 20);
      ctx.fillText(`Max Depth: ${treeInfo.maxDepth}`, width - 10, 35);
      ctx.fillText(`Strategies: ${treeInfo.partitionStrategies.length}`, width - 10, 50);
    }
  };

  useEffect(() => {
    drawTree();
  }, [showLabels, highlightedNode, treeInfo]);

  // Handle canvas resize
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const resizeCanvas = () => {
      const container = canvas.parentElement;
      if (container) {
        canvas.width = container.clientWidth;
        canvas.height = 300;
        drawTree();
      }
    };

    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    return () => window.removeEventListener('resize', resizeCanvas);
  }, []);

  // Handle canvas clicks for node interaction
  const handleCanvasClick = (event: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const rect = canvas.getBoundingClientRect();
    const x = event.clientX - rect.left;
    const y = event.clientY - rect.top;

    // Simple hit detection for demonstration
    const tree = createTreeStructure();
    const centerX = canvas.width / 2;
    const centerY = 80;
    calculateNodePositions(tree, centerX, centerY, 0, 0, 1);

    const checkNodeHit = (node: TreeNode): string | null => {
      const distance = Math.sqrt(Math.pow(x - node.x, 2) + Math.pow(y - node.y, 2));
      if (distance <= 25) {
        return node.id;
      }
      
      for (const child of node.children) {
        const childHit = checkNodeHit(child);
        if (childHit) return childHit;
      }
      
      return null;
    };

    const hitNode = checkNodeHit(tree);
    setHighlightedNode(hitNode);
  };

  return (
    <Card>
      <CardContent>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
          <Typography variant="h6">
            BSP Tree Structure
          </Typography>
          
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <FormControlLabel
              control={
                <Switch
                  checked={showLabels}
                  onChange={(e) => setShowLabels(e.target.checked)}
                  size="small"
                />
              }
              label="Show Labels"
            />
            
            {highlightedNode && (
              <Chip
                label={`Selected: ${highlightedNode}`}
                size="small"
                onDelete={() => setHighlightedNode(null)}
              />
            )}
          </Box>
        </Box>

        <Box sx={{ border: '1px solid #e0e0e0', borderRadius: 1, overflow: 'hidden' }}>
          <canvas
            ref={canvasRef}
            style={{ display: 'block', width: '100%', height: '300px', cursor: 'pointer' }}
            onClick={handleCanvasClick}
          />
        </Box>

        <Box sx={{ mt: 2 }}>
          <Typography variant="body2" color="text.secondary">
            This visualization shows the hierarchical structure of the BSP tree used for musical analysis.
            Click on nodes to highlight them and explore the tree structure.
          </Typography>
          
          {treeInfo && (
            <Box sx={{ mt: 1, display: 'flex', gap: 1, flexWrap: 'wrap' }}>
              <Chip label={`${treeInfo.totalRegions} regions`} size="small" />
              <Chip label={`Depth: ${treeInfo.maxDepth}`} size="small" />
              <Chip label={`${treeInfo.partitionStrategies.length} strategies`} size="small" />
            </Box>
          )}
        </Box>
      </CardContent>
    </Card>
  );
};
