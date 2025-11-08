# GEMINI.md - Guitar Alchemist

This document provides a comprehensive overview of the Guitar Alchemist project, its architecture, and development conventions, intended to be used as a guide for Gemini and other AI agents.

## Project Overview

Guitar Alchemist is an AI-powered music theory and guitar learning platform. It combines a rich music theory engine with a modern web application to provide an interactive and intelligent learning experience. The project is built on a diverse technology stack, including .NET 9, React, and various AI technologies.

### Key Technologies

*   **.NET 9:** The backend is built on .NET 9, with a mix of C# and F# projects. It uses ASP.NET Core for the main API and leverages modern .NET features like minimal APIs and structured concurrency.
*   **React:** The frontend is a single-page application (SPA) built with React and TypeScript. It uses Material-UI for UI components and Jotai for state management.
*   **Databases:** The project uses both MongoDB and SQLite. MongoDB is the primary database for storing musical data, while SQLite is used for caching.
*   **AI/ML:** The project heavily utilizes AI and machine learning. It uses Ollama for local language model hosting, `Microsoft.Extensions.AI` for AI integration, and has integrations with Hugging Face for music generation.
*   **Docker:** The entire application is containerized using Docker, and `docker-compose.yml` is provided for easy orchestration of the services.
*   **Aspire:** The project uses .NET Aspire for cloud-native orchestration and service discovery.

### Architecture

The project follows a microservices-oriented architecture, with a clear separation of concerns between the different parts of the application.

*   **`ga-server`:** The main backend service, which exposes a REST API, a GraphQL API, and SignalR hubs for real-time communication.
*   **`ga-client`:** The React-based frontend application.
*   **`GuitarAlchemistChatbot`:** A dedicated service for the AI chatbot.
*   **`Common` libraries:** A set of shared libraries that contain the core business logic, music theory engine, and data access code.
*   **Python services:** Several Python-based microservices for tasks like hand pose detection and sound synthesis.

## Building and Running

The following commands are used to build, run, and test the project.

### Setup

To set up the development environment, run the following command from the root of the project:

```powershell
pwsh Scripts/setup-dev-environment.ps1
```

This script will install all the necessary dependencies, including .NET SDKs, Node.js modules, and Python packages.

### Running the Application

To start all the services, including the backend, frontend, and databases, run the following command:

```powershell
pwsh Scripts/start-all.ps1 -Dashboard
```

This will start all the services and open the Aspire dashboard, where you can monitor the status of the different services.

### Running Tests

To run all the tests, including backend and frontend tests, run the following command:

```powershell
pwsh Scripts/run-all-tests.ps1
```

You can also run specific sets of tests:

*   **Backend only:** `pwsh Scripts/run-all-tests.ps1 -BackendOnly`
*   **Playwright UI tests:** `pwsh Scripts/run-all-tests.ps1 -PlaywrightOnly`

## Development Conventions

The project follows a set of development conventions to ensure code quality and consistency.

### Code Style

*   **C#:** The C# code follows the `.editorconfig` file in the root of the project. It uses 4-space indentation, file-scoped namespaces, `PascalCase` for types and methods, and `camelCase` for locals and parameters.
*   **TypeScript/React:** The frontend code follows the standard React conventions. It uses functional components, PascalCase for filenames, and `npm run lint` for linting.

### Testing

*   The project has a comprehensive test suite, with unit tests, integration tests, and end-to-end tests.
*   Backend tests are written using NUnit and xUnit, and can be found in the `Tests` directory.
*   Frontend tests are written using Playwright and can be found in the `Apps/ga-client/tests` directory.

### Commits and Pull Requests

*   The project uses Conventional Commits for commit messages.
*   Pull requests should include a summary of the changes, a link to the relevant issue, and the output of the tests.

### Security

*   Secrets should be managed using `dotnet user-secrets` or environment variables.
*   The `docs` and `Specs` directories should be audited for any accidental leaks of sensitive information.
