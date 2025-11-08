# Guitar Alchemist

An AI-powered music theory and guitar learning platform built with .NET 9, React, and cutting-edge AI technologies.

## 🚀 Quick Start

See [AGENTS.md](./AGENTS.md) for complete setup and development guidelines.

```powershell
# Setup development environment
pwsh Scripts/setup-dev-environment.ps1

# Start all services with Aspire dashboard
pwsh Scripts/start-all.ps1 -Dashboard

# Run tests
pwsh Scripts/run-all-tests.ps1
```

## 📚 Documentation

### Core Documentation

- **[AGENTS.md](./AGENTS.md)** - Repository guidelines, project structure, and development workflow
- **[AI Future Roadmap](./docs/AI_FUTURE_ROADMAP.md)** - Vision for AI-powered features and multimodal learning
- **[AI-Ready API Implementation](./docs/AI_READY_API_IMPLEMENTATION.md)** - API design principles for AI agents
- **[AI Music Generation Services](./docs/AI_MUSIC_GENERATION_SERVICES.md)** - Music generation and synthesis
  capabilities

### Architecture

- **[ChatGPT-LLMs for Music Generation](./ChatGPT-LLMs%20for%20music%20generation.md)** - LLM integration for music
  theory

## 🎯 Key Features

- **Music Theory Engine** - Comprehensive chord, scale, and progression analysis
- **AI Chatbot** - Interactive music theory assistant powered by GPT-4
- **Semantic Search** - Vector-based chord and scale discovery with MongoDB
- **Real-Time Analysis** - Hand pose detection and guitar technique coaching (planned)
- **Voice Integration** - Voice-enabled tutoring and commands (planned)
- **Monadic APIs** - Type-safe error handling with Option/Result/Try patterns

## 🏗️ Project Structure

```
ga/
├── Apps/                          # Runtime applications
│   ├── ga-server/GaApi/          # Main REST/GraphQL API
│   ├── GuitarAlchemistChatbot/   # AI chatbot service
│   └── ga-client/                # React frontend
├── Common/                        # Core libraries
│   ├── GA.Business.Core/         # Business logic
│   ├── GA.MusicTheory.DSL/       # Music theory domain
│   └── GA.Data.MongoDB/          # Data access
├── Tests/                         # Test suites
├── docs/                          # Documentation
└── Scripts/                       # Build and deployment scripts
```

## 🤖 AI Roadmap

Guitar Alchemist is evolving into a comprehensive AI-powered music learning platform. See our *
*[AI Future Roadmap](./docs/AI_FUTURE_ROADMAP.md)** for details on:

- **Phase 2** (Next 3-6 months): Real-time multimodal intelligence with Vision Agents, OpenVoice v2, and SpeechBrain
- **Phase 3** (6-12 months): Audio analysis, generative music AI, and collaborative jamming
- **Phase 4** (12+ months): Tutorial generation and adaptive learning platform

## 🛠️ Technology Stack

- **.NET 9** - Backend services and APIs
- **React + TypeScript** - Frontend UI
- **MongoDB** - Database with vector search
- **Aspire** - Cloud-native orchestration
- **OpenAI GPT-4** - AI chatbot and semantic analysis
- **Semantic Kernel** - AI orchestration
- **Python/FastAPI** - AI microservices (pose detection, sound synthesis)

## 📖 Getting Started

1. **Prerequisites**: .NET 9 SDK, Node.js 20+, MongoDB, Docker
2. **Setup**: Run `pwsh Scripts/setup-dev-environment.ps1`
3. **Development**: Run `pwsh Scripts/start-all.ps1 -Dashboard`
4. **Access**:
    - API: https://localhost:7001
    - Aspire Dashboard: https://localhost:15001
    - Chatbot: https://localhost:7002

## 🧪 Testing

```powershell
# Run all tests
dotnet test AllProjects.sln

# Backend only
pwsh Scripts/run-all-tests.ps1 -BackendOnly

# Playwright UI tests
pwsh Scripts/run-all-tests.ps1 -PlaywrightOnly
```

## 📝 Contributing

See [AGENTS.md](./AGENTS.md) for:

- Code style guidelines
- Commit conventions
- Testing requirements
- Pull request process

## 📄 License

[Add license information here]

## 🔗 Links

- [Aspire Dashboard](https://localhost:15001) - Service monitoring
- [Jaeger](http://localhost:16686) - Distributed tracing
- [Mongo Express](http://localhost:8081) - Database UI
