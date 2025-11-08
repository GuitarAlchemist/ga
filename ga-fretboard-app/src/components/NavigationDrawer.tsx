import React from 'react';
import { useAtom } from 'jotai';
import {
  Drawer,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  IconButton,
  Divider,
  Box,
  Typography,
} from '@mui/material';
import {
  ChevronLeft as ChevronLeftIcon,
  ChevronRight as ChevronRightIcon,
  MusicNote as MusicNoteIcon,
  Piano as PianoIcon,
  LibraryMusic as LibraryMusicIcon,
  Settings as SettingsIcon,
} from '@mui/icons-material';
import { drawerOpenAtom, selectedMenuItemAtom } from '../store/atoms';

const DRAWER_WIDTH = 240;
const DRAWER_WIDTH_COLLAPSED = 60;

interface MenuItem {
  id: string;
  label: string;
  icon: React.ReactNode;
}

const menuItems: MenuItem[] = [
  { id: 'fretboard', label: 'Fretboard', icon: <MusicNoteIcon /> },
  { id: 'chords', label: 'Chords', icon: <PianoIcon /> },
  { id: 'scales', label: 'Scales', icon: <LibraryMusicIcon /> },
  { id: 'settings', label: 'Settings', icon: <SettingsIcon /> },
];

/**
 * NavigationDrawer Component
 * 
 * A collapsible navigation menu with a navy color scheme.
 * Uses Jotai for state management.
 */
const NavigationDrawer: React.FC = () => {
  const [drawerOpen, setDrawerOpen] = useAtom(drawerOpenAtom);
  const [selectedMenuItem, setSelectedMenuItem] = useAtom(selectedMenuItemAtom);

  const handleDrawerToggle = () => {
    setDrawerOpen(!drawerOpen);
  };

  const handleMenuItemClick = (itemId: string) => {
    setSelectedMenuItem(itemId);
  };

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: drawerOpen ? DRAWER_WIDTH : DRAWER_WIDTH_COLLAPSED,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: drawerOpen ? DRAWER_WIDTH : DRAWER_WIDTH_COLLAPSED,
          boxSizing: 'border-box',
          backgroundColor: '#001f3f', // Navy color
          color: '#fff',
          transition: 'width 0.3s ease',
          overflowX: 'hidden',
        },
      }}
    >
      {/* Header */}
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: drawerOpen ? 'space-between' : 'center',
          p: 2,
          minHeight: 64,
        }}
      >
        {drawerOpen && (
          <Typography variant="h6" noWrap component="div" sx={{ color: '#fff' }}>
            Guitar Alchemist
          </Typography>
        )}
        <IconButton onClick={handleDrawerToggle} sx={{ color: '#fff' }}>
          {drawerOpen ? <ChevronLeftIcon /> : <ChevronRightIcon />}
        </IconButton>
      </Box>

      <Divider sx={{ backgroundColor: 'rgba(255, 255, 255, 0.12)' }} />

      {/* Menu Items */}
      <List>
        {menuItems.map((item) => (
          <ListItem key={item.id} disablePadding>
            <ListItemButton
              selected={selectedMenuItem === item.id}
              onClick={() => handleMenuItemClick(item.id)}
              sx={{
                minHeight: 48,
                justifyContent: drawerOpen ? 'initial' : 'center',
                px: 2.5,
                '&.Mui-selected': {
                  backgroundColor: 'rgba(255, 255, 255, 0.16)',
                  '&:hover': {
                    backgroundColor: 'rgba(255, 255, 255, 0.24)',
                  },
                },
                '&:hover': {
                  backgroundColor: 'rgba(255, 255, 255, 0.08)',
                },
              }}
            >
              <ListItemIcon
                sx={{
                  minWidth: 0,
                  mr: drawerOpen ? 3 : 'auto',
                  justifyContent: 'center',
                  color: '#fff',
                }}
              >
                {item.icon}
              </ListItemIcon>
              <ListItemText
                primary={item.label}
                sx={{
                  opacity: drawerOpen ? 1 : 0,
                  color: '#fff',
                }}
              />
            </ListItemButton>
          </ListItem>
        ))}
      </List>
    </Drawer>
  );
};

export default NavigationDrawer;

