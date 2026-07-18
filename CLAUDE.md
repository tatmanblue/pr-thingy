# Technology

- C# / .NET 10
- Avalonia UI (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`) — cross-platform desktop GUI (Windows/macOS/Linux), including system tray support via `TrayIcon`
- `CommunityToolkit.Mvvm` — MVVM (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`)
- `Microsoft.Extensions.Hosting` — DI container and `BackgroundService` for the in-process PR-polling loop
- `System.Text.Json` — structured briefing persistence and agent-response parsing
- Shells out to the `gh` CLI (GitHub CLI, user-installed) for PR data — `gh pr list`, `gh pr view`, `gh pr diff`
- Shells out to the `claude` and `gemini` CLIs (user-installed) to generate PR briefings, behind an `IAgentClient` interface/DI abstraction so either can be swapped or extended
- File-based storage for settings and briefings, behind interface abstractions (`IBriefingRepository`, `IWatchedRepositoryStore`, `IAppSettingsStore`) so a database implementation could replace it later without touching callers
- xUnit + Moq for tests

# Coding Conventions

- Do not use `_` as a prefix for class-level fields. Use plain camelCase (e.g., `registryFilePath`).
- When a constructor parameter name collides with a field name, disambiguate with `this.` (e.g., `this.registry = registry`).
- Constants should be ALL_CAPS_WITH_UNDERSCORES.

# Instructions for Claude
All changes must be approved before creating these changes. Please prepare a plan of proposed changes and get confirmation before proceeding.
