# Guitar Alchemist Chatbot - Enhanced Features Documentation

## Overview

The Guitar Alchemist Chatbot has been enhanced with advanced features including conversation context persistence, web
integration, enhanced function calling, and comprehensive testing.

---

## 🎯 **New Features**

### 1. **Guitar Tab Visualization** ✅

The chatbot already includes VexTab integration for displaying guitar tablature notation.

**Features:**

- ASCII tab format rendering
- Graphical fretboard diagrams via VexTab
- Interactive tab display within chat messages
- Support for standard notation and tablature

**Usage:**

```
User: "Show me a C major scale in tab notation"
User: "Display chord voicings for Cmaj7"
```

**Example:**
Click the "Show guitar TAB example" button to see a sample tab rendering.

---

### 2. **Enhanced Short-Term Memory** 🧠

The chatbot now maintains conversation context throughout your session.

**Features:**

- Tracks recent user queries
- Remembers referenced chords and scales
- Stores music theory concepts discussed
- Displays context summary in the header
- Enables natural references like "the chord I mentioned earlier"

**Context Tracking:**

- **Last Chord**: Remembers the most recently discussed chord
- **Last Scale**: Tracks the current scale being discussed
- **Recent Topics**: Maintains a history of conversation topics
- **Preferences**: Stores user preferences during the session

**Usage:**

```
User: "Tell me about Cmaj7"
Bot: [Explains Cmaj7]
User: "What are similar chords to that one?"
Bot: [Understands "that one" refers to Cmaj7]
```

**Context Indicator:**
Look for the brain icon (🧠) in the chat header showing your current context:

- "Last chord: Cmaj7"
- "Recent topics: jazz chords, modes"

**Clearing Context:**
Click "New Chat" to start fresh and clear all context.

---

### 3. **Enhanced Function Calling Integration** 🔧

Visual indicators and structured display for AI function invocations.

**Features:**

- Real-time function call indicators
- Loading states with function names
- Structured result formatting
- Error handling with graceful fallbacks

**Available Functions:**

#### **Chord Functions:**

- `SearchChords` - Natural language chord search
- `FindSimilarChords` - Find alternatives to a chord
- `GetChordDetails` - Detailed chord information
- `ExplainMusicTheory` - Music theory explanations

#### **Web Integration Functions:**

- `SearchWikipedia` - Search Wikipedia for music theory
- `GetWikipediaSummary` - Get detailed Wikipedia summaries
- `SearchMusicTheorySite` - Search specific music theory websites
- `GetLatestMusicLessons` - Fetch latest lessons from RSS feeds
- `FetchMusicTheoryArticle` - Extract content from articles

**Function Indicators:**
When a function is called, you'll see:

```
🔧 Calling SearchChords... ⏳
```

**Usage:**

```
User: "Find me some dark jazz chords"
[Function Indicator: Calling SearchChords...]
Bot: [Displays structured chord results]

User: "Search Wikipedia for harmonic minor scale"
[Function Indicator: Calling SearchWikipedia...]
Bot: [Displays Wikipedia search results]
```

---

### 4. **MCP Tools Integration** 🌐

Integration with external web sources for enhanced knowledge.

**Web Search:**

- Wikipedia search and summaries
- DuckDuckGo search integration
- Music theory website search (musictheory.net, justinguitar.com, etc.)

**RSS Feed Reading:**

- Latest lessons from JustinGuitar
- Music theory articles
- Guitar technique tutorials

**Web Scraping:**

- Extract content from music theory articles
- Domain-whitelisted for security
- Content sanitization

**Caching:**

- Web pages cached for 2 hours
- RSS feeds cached for 30 minutes
- Wikipedia searches cached for 24 hours
- Reduces external requests by 80-90%

**Usage Examples:**

```
User: "Search Wikipedia for circle of fifths"
User: "What are the latest guitar lessons from JustinGuitar?"
User: "Search musictheory.net for chord progressions"
User: "Get me a summary about Dorian mode from Wikipedia"
```

**Supported Sources:**

- Wikipedia (en.wikipedia.org)
- MusicTheory.net
- JustinGuitar.com
- GuitarNoise.com
- Teoria.com

---

## 🎨 **User Interface Enhancements**

### **Context Indicator**

- Located in the chat header
- Shows current conversation context
- Displays last chord, scale, or topics
- Updates after each message

### **Function Call Indicator**

- Appears during function execution
- Shows function name being called
- Animated loading spinner
- Disappears when function completes

### **Structured Results**

- Chord results displayed in formatted lists
- Web search results with proper formatting
- Bold and italic text for emphasis
- Organized bullet points and numbered lists

---

## 🧪 **Testing**

### **Playwright Test Suite**

Comprehensive automated tests covering all features:

**Test Categories:**

1. **Tab Viewer Tests** - VexTab rendering across browsers
2. **Context Persistence Tests** - Conversation memory
3. **Function Calling Tests** - AI function integration
4. **MCP Integration Tests** - Web search and feeds

**Running Tests:**

```bash
# Install Playwright browsers (first time only)
pwsh Tests/GuitarAlchemistChatbot.Tests.Playwright/bin/Debug/net9.0/playwright.ps1 install

# Run all tests
dotnet test Tests/GuitarAlchemistChatbot.Tests.Playwright/GuitarAlchemistChatbot.Tests.Playwright.csproj

# Run specific test category
dotnet test --filter "FullyQualifiedName~TabViewerTests"
dotnet test --filter "FullyQualifiedName~ContextPersistenceTests"
dotnet test --filter "FullyQualifiedName~FunctionCallingTests"
dotnet test --filter "FullyQualifiedName~McpIntegrationTests"

# Run with specific browser
dotnet test -- Playwright.BrowserName=firefox
dotnet test -- Playwright.BrowserName=webkit
```

**Test Coverage:**

- ✅ VexTab rendering in multiple browsers
- ✅ Conversation context persistence
- ✅ Function call indicators
- ✅ Web search integration
- ✅ RSS feed reading
- ✅ Error handling
- ✅ Responsive design
- ✅ Performance benchmarks

---

## 🔒 **Security Features**

### **Domain Whitelisting**

Only approved music theory domains are accessible:

- musictheory.net
- wikipedia.org
- justinguitar.com
- guitarnoise.com
- ultimate-guitar.com
- songsterr.com
- teoria.com

### **Content Sanitization**

- Scripts and styles removed
- Ads and navigation filtered
- HTML entity decoding
- Input validation

### **Rate Limiting**

- Caching prevents excessive requests
- Timeout protection
- Error handling for unreliable sources

---

## 📊 **Performance**

### **Caching Strategy**

- **Web Pages**: 2 hours
- **RSS Feeds**: 30 minutes
- **DuckDuckGo**: 6 hours
- **Wikipedia Search**: 24 hours
- **Wikipedia Summary**: 7 days

### **Benefits:**

- 80-90% reduction in external requests
- Faster response times
- Reduced bandwidth usage
- Better user experience

---

## 🚀 **Getting Started**

### **Prerequisites**

- .NET 9.0 SDK
- Modern web browser
- Internet connection (for web integration features)

### **Running the Chatbot**

```bash
# Navigate to the project directory
cd Apps/GuitarAlchemistChatbot

# Run the application
dotnet run

# Open browser to https://localhost:7001
```

### **Configuration**

**Optional OpenAI API Key:**
Add to `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here"
  }
}
```

**Note:** The chatbot works fully without an API key using the built-in demo mode.

---

## 💡 **Usage Tips**

### **Making the Most of Context**

1. Have natural conversations - the bot remembers!
2. Use references like "that chord" or "the scale I mentioned"
3. Check the context indicator to see what's remembered
4. Click "New Chat" to start fresh

### **Effective Function Usage**

1. Be specific in your requests
2. Combine multiple requests in one message
3. Watch for function indicators to see what's happening
4. Results are formatted for easy reading

### **Web Integration**

1. Ask for Wikipedia summaries for detailed information
2. Request latest lessons to stay updated
3. Search specific sites for targeted information
4. Combine web search with chord search for comprehensive answers

---

## 🐛 **Troubleshooting**

### **VexTab Not Rendering**

- Ensure JavaScript is enabled
- Check browser console for errors
- Try refreshing the page
- Verify VexTab libraries are loaded

### **Context Not Persisting**

- Context clears on "New Chat"
- Context is session-based (not saved between visits)
- Check context indicator for current state

### **Function Calls Failing**

- Check internet connection for web integration
- Verify domain is whitelisted
- Check browser console for errors
- Try a different query

### **Slow Performance**

- First requests may be slower (caching)
- Subsequent requests should be faster
- Check network connection
- Clear browser cache if needed

---

## 📚 **Additional Resources**

### **Related Documentation**

- `README.md` - Project overview
- `WEB_INTEGRATION_SUMMARY.md` - Web integration details
- `CODE_SHARING_ARCHITECTURE.md` - Architecture documentation

### **External Resources**

- [VexTab Documentation](http://vexflow.com/vextab/)
- [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/)
- [Playwright Documentation](https://playwright.dev/dotnet/)

---

## 🎵 **Example Conversations**

### **Example 1: Chord Exploration with Context**

```
User: "Tell me about Cmaj7"
Bot: [Explains Cmaj7 chord]
Context: Last chord: Cmaj7

User: "What are similar chords?"
Bot: [Suggests Dm7, Em7, Fmaj7, etc.]
Context: Last chord: Dm7 | Recent topics: Cmaj7, similar chords

User: "Show me the first one in tab notation"
Bot: [Displays Dm7 tab]
```

### **Example 2: Web-Enhanced Learning**

```
User: "Search Wikipedia for Dorian mode"
[Function: Calling SearchWikipedia...]
Bot: [Displays Wikipedia search results]

User: "Get me a detailed summary"
[Function: Calling GetWikipediaSummary...]
Bot: [Displays comprehensive summary]

User: "Now find me chords that work with Dorian"
[Function: Calling SearchChords...]
Bot: [Displays relevant chord suggestions]
```

### **Example 3: Latest Lessons**

```
User: "What are the latest guitar lessons?"
[Function: Calling GetLatestMusicLessons...]
Bot: [Displays recent lessons from JustinGuitar]

User: "Tell me more about the first one"
Bot: [Provides details about the lesson]
```

---

**Happy Music Making! 🎸🎵**

