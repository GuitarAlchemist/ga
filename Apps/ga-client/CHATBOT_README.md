# Guitar Alchemist React Chatbot

## Overview

A modern React/TypeScript/Jotai chatbot interface for Guitar Alchemist with VexFlow music notation rendering capabilities.

## Features

✅ **React/TypeScript/Jotai Architecture**
- Modern React functional components with TypeScript
- Jotai for lightweight state management
- Persistent chat history using `atomWithStorage`

✅ **Rich Message Rendering**
- Markdown support with `react-markdown`
- Syntax highlighting for code blocks
- **VexTab/VexFlow Integration** - Render guitar tablature and music notation
- Chord diagram support
- Copy message functionality

✅ **Interactive Chat Interface**
- Real-time streaming responses with typing indicators
- Keyboard shortcuts (Enter to send, Shift+Enter for new line)
- Auto-scroll to latest messages
- Quick suggestion chips for common queries
- Clear chat history

✅ **Material-UI Design**
- Clean, modern interface with MUI components
- Responsive layout
- Dark/light theme support
- Accessible components

## Architecture

### Components

```
Apps/ga-client/src/components/Chat/
├── ChatInterface.tsx    # Main chat container
├── ChatMessage.tsx      # Individual message rendering
└── ChatInput.tsx        # Message input with keyboard shortcuts
```

### State Management (Jotai Atoms)

```typescript
Apps/ga-client/src/store/chatAtoms.ts

// Core atoms
- chatMessagesAtom          # Persistent message history
- chatInputAtom             # Current input text
- isLoadingAtom             # Loading state
- currentStreamingMessageAtom # Streaming message state
- chatConfigAtom            # API configuration

// Derived atoms
- visibleMessagesAtom       # Filtered messages for display

// Actions
- addMessageAtom            # Add new message
- clearMessagesAtom         # Clear history
```

## VexTab/VexFlow Support

The chatbot can render guitar tablature and music notation using VexFlow:

### Example Usage

**User:** "Show me a C major scale in tab"

**Assistant:**
````markdown
Here's a C major scale:

```vextab
tabstave notation=true tablature=true
notes :q 3/5 0/4 2/4 3/4 | 0/3 2/3 3/3 5/3
text :q,C,D,E,F,G,A,B,C
```
````

This renders as interactive music notation with both standard notation and tablature.

### VexTab Syntax

```
tabstave notation=true tablature=true
notes :q 0/6 3/6 0/5 2/5
text :q,E,G,A,B
```

- `tabstave` - Define the staff
- `notation=true` - Show standard notation
- `tablature=true` - Show guitar tab
- `notes :q` - Quarter notes
- `3/5` - Fret 3 on string 5
- `text` - Note names

## Running the Chatbot

### Development

```bash
cd Apps/ga-client
npm install
npm run dev
```

Navigate to `http://localhost:5173` and click the **"AI Chat Assistant"** tab.

### Production Build

```bash
npm run build
npm run preview
```

## Current Capabilities

### Simulated Responses

Currently, the chatbot uses simulated responses for demonstration. It can:

1. **VexTab Examples** - Show guitar tablature notation
2. **Chord Diagrams** - Display chord fingerings
3. **Music Theory** - Explain concepts
4. **Quick Suggestions** - Provide common queries

### Keywords Recognized

- `tab`, `vextab` → Shows VexTab example
- `chord` + `c` → Shows C major chord diagram
- Default → Shows welcome message with capabilities

## Future Enhancements

### API Integration (Planned)

```typescript
// Replace simulateAIResponse with real API call
const response = await fetch('http://localhost:5000/api/chat', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    messages: messages,
    stream: true
  })
});

// Handle streaming response
const reader = response.body.getReader();
// ... stream processing
```

### Additional Features

- [ ] Real-time API integration with GaApi backend
- [ ] Function calling for chord search, music theory queries
- [ ] Voice input support
- [ ] Export chat history
- [ ] Share conversations
- [ ] Custom themes
- [ ] Mobile-optimized layout
- [ ] Offline mode with service workers

## Dependencies

```json
{
  "react": "^18.3.1",
  "react-dom": "^18.3.1",
  "jotai": "^2.15.0",
  "@mui/material": "^5.15.12",
  "@mui/icons-material": "^5.15.12",
  "react-markdown": "^9.0.1",
  "react-syntax-highlighter": "^15.5.0",
  "ga-react-components": "file:../../ReactComponents/ga-react-components"
}
```

## Integration with Existing Components

The chatbot integrates seamlessly with existing Guitar Alchemist components:

- **VexTabViewer** - From `ga-react-components` for music notation
- **VexChordDiagram** - For chord diagrams
- **Jotai atoms** - Shared state management with main app

## Tab Navigation

The app now features tab navigation:

1. **Fretboard Explorer** - Original fretboard visualization
2. **AI Chat Assistant** - New chatbot interface

Switch between tabs using the top navigation bar.

## Customization

### Modify Welcome Message

Edit `Apps/ga-client/src/store/chatAtoms.ts`:

```typescript
export const chatMessagesAtom = atomWithStorage<ChatMessage[]>('ga-chat-messages', [
  {
    id: 'system-welcome',
    role: 'system',
    content: 'Your custom welcome message here!',
    timestamp: new Date(),
  },
]);
```

### Add Quick Suggestions

Edit `Apps/ga-client/src/components/Chat/ChatInterface.tsx`:

```typescript
const quickSuggestions = [
  'Your custom suggestion 1',
  'Your custom suggestion 2',
  // ...
];
```

### Customize Theme

Edit `Apps/ga-client/src/App.tsx`:

```typescript
const defaultTheme = createTheme({
  palette: {
    mode: 'dark', // or 'light'
    primary: {
      main: '#your-color',
    },
    // ...
  },
});
```

## Troubleshooting

### VexTab Not Rendering

1. Check browser console for errors
2. Verify VexFlow is loaded in `ga-react-components`
3. Ensure VexTab syntax is correct

### Messages Not Persisting

- Check browser localStorage
- Clear cache if needed: `localStorage.removeItem('ga-chat-messages')`

### Styling Issues

- Verify MUI theme is properly configured
- Check for CSS conflicts
- Ensure all MUI components are imported

## Contributing

When adding new features:

1. Update Jotai atoms in `chatAtoms.ts`
2. Create new components in `components/Chat/`
3. Update this README
4. Add TypeScript types
5. Test with different message types

## License

Part of the Guitar Alchemist project.

