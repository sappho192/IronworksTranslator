# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IronworksTranslator is a Windows WPF application that provides real-time Korean translation of chat messages in Final Fantasy XIV. The application uses multiple translation engines including DeepL API, Papago, and a custom AI-based Japanese-Korean translator.

## Build and Development Commands

### Building the Application
```bash
# Build in Release configuration
dotnet build src/IronworksTranslator/IronworksTranslator.csproj -c Release

# Build and publish for distribution
dotnet publish src/IronworksTranslator/IronworksTranslator.csproj -p:PublishProfile=FolderProfile
```

### Running the Application
```bash
# Run from project directory
cd src/IronworksTranslator
dotnet run
```

The application targets .NET 8.0 Windows and uses WPF. No test projects are currently available in this codebase.

## Architecture Overview

### Application Structure
- **WPF + MVVM Pattern**: Uses CommunityToolkit.Mvvm for ObservableObject and RelayCommand
- **Dependency Injection**: Microsoft.Extensions.Hosting with DI container configured in App.xaml.cs
- **Game Integration**: Uses Sharlayan.Lite library for FFXIV memory reading
- **Translation Engines**: Multiple translation services (DeepL, Papago, custom AI model)
- **Localization**: Lepo.i18n with YAML-based string resources (Korean/English)

### Key Architectural Components

#### Memory Management & Game Integration
- `ChatLookupService`: Hosted service that monitors FFXIV chat log via memory reading
- `MinimizationLookupService`: Manages window minimization behavior  
- `MemoryHandler`: Sharlayan-based FFXIV process attachment and memory access

#### Translation System
- `TranslatorBase`: Abstract base class for all translation engines
- `AihubJaKoTranslator`: Custom ONNX-based Japanese-Korean translator using EDMTranslator
- `DeepLAPITranslator`: DeepL API integration
- `PapagoTranslator`: Naver Papago API integration
- Translation engines are registered as singletons in DI container

#### UI Layer (MVVM)
- **ViewModels**: Located in `ViewModels/` (Pages and Windows subdirectories)
- **Views**: Located in `Views/` with matching structure
- **Models**: Business objects in `Models/` including Settings, Enums, Chat structures
- Uses WPF-UI library for modern UI controls and theming

#### Configuration & Settings
- YAML-based settings system with `YamlDotNet`
- `IronworksSettings`: Main settings class with nested settings objects
- Settings automatically persist on change via `SaveSettingsOnChange` aspect
- Localized strings stored in `Resources/Strings/` as YAML files

### Directory Structure
```
src/IronworksTranslator/
├── Models/           # Business objects, enums, settings
├── ViewModels/       # MVVM ViewModels (Pages/, Windows/)
├── Views/           # WPF Views (Pages/, Windows/)  
├── Services/        # Background services, game integration
├── Utils/           # Utilities, translators, UI helpers
├── Helpers/         # Converters, extensions
├── Resources/       # Localization strings, assets
└── data/           # Translation models, dictionaries
```

### Key Dependencies
- **WPF-UI**: Modern WPF controls and theming
- **Sharlayan.Lite**: FFXIV memory reading
- **EDMTranslator**: Custom AI translation models
- **DeepL.net**: DeepL API client
- **Serilog**: Logging framework
- **CommunityToolkit.Mvvm**: MVVM helpers
- **YamlDotNet**: YAML configuration

### WatchDog Process
The application includes a separate `WatchDogMain.exe` process that monitors the main application for crashes and restarts it if needed. This is launched during startup in `App.xaml.cs`.

### Translation Model Management
The application can download and use large AI translation models (1GB+). The `AihubJaKoTranslator` uses ONNX models and tokenizers that are either bundled or downloaded on first run.