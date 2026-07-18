# Setup and troubleshooting

This doc covers what needs to be installed and authenticated before pr-thingy can do anything
useful, and how to translate what the app shows you — the startup banner, the sync log panel, and
console output — into an actual fix.

## What's required

- **.NET 10 SDK** — to build and run the app.
- **GitHub CLI (`gh`)**, installed *and authenticated*. Run `gh auth login`, or set the `GH_TOKEN`
  environment variable to a valid token. This must cover every account/org whose repos you watch —
  `gh pr list` runs per watched repo, using whatever `gh` identity is active.
- **An agent CLI** — [Claude Code](https://docs.claude.com/en/docs/claude-code) (`claude`) and/or
  the [Gemini CLI](https://github.com/google-gemini/gemini-cli) (`gemini`), matching whichever
  agent you select in Settings. The Gemini CLI additionally requires Node.js 20+.

  Note: the startup check only requires *one* of the two to be reachable, not specifically the one
  you've selected. If you've chosen Claude in Settings but only Gemini is actually on PATH (or vice
  versa), the startup banner will stay quiet while every sync still fails for that repo — see
  "agent invocation failed" below.
- **Local git clones** with an `origin` remote pointing at the GitHub repo you want to watch — a
  bare GitHub URL isn't enough, pr-thingy runs `git fetch` and `gh` commands inside the clone's
  directory.

### Windows: the PATH gotcha

CLI tools installed via npm, pip, or a vendor installer sometimes only end up on PATH inside your
interactive shell's profile (or a one-off session an installer set up) — not in the *persisted*
Windows User or Machine PATH environment variable. pr-thingy launches `gh`/`claude`/`gemini`
directly via .NET's `Process.Start`, not through a shell, so it only ever sees the **persisted**
PATH — never whatever your terminal profile added at login.

**Symptom:** the tool works fine when you type it into a terminal, but the app's sync log (or
console output) shows something like:

```
System.ComponentModel.Win32Exception (2): An error occurred trying to start process 'claude' ...
The system cannot find the file specified.
```

**Fix:** find where the tool actually lives (`Get-Command claude` in PowerShell), then add its
containing folder to your *persisted* User PATH — Windows Settings → "Edit environment variables
for your account" → edit `Path` → add the folder — then restart any terminal and pr-thingy so the
new processes pick it up.

Concrete example hit during development: `claude.exe` was installed at
`C:\Users\<user>\.local\bin\claude.exe`, and that folder was present in the live shell's PATH but
missing from `[Environment]::GetEnvironmentVariable('Path','User')` — so a terminal could run
`claude` fine, but pr-thingy (launched independently) couldn't find it.

## What the app shows, and what it means

### Startup warning banner

A dismissible banner at the top of the main window, populated once at launch by
`IStartupEnvironmentChecker`. It checks tool *presence* and, for `gh`, authentication — not
whether your specifically-selected agent works (see the note above). Possible messages:

- `Missing required tool: GitHub CLI (gh). See the README for setup details.`
  → `gh` isn't on PATH at all.
- `Missing required tool: GitHub CLI authentication (run 'gh auth login'). See the README for setup details.`
  → `gh` is installed, but `gh auth status` failed.
- `Missing required tool: an agent CLI (claude or gemini). See the README for setup details.`
  → neither `claude` nor `gemini` is reachable.
- `Missing required tools: X and Y. See the README for setup details.`
  → more than one of the above at once.

The banner is only evaluated on launch — after installing or authenticating something, restart the
app rather than expecting the banner to clear itself.

### Sync log panel

The right-hand panel is a running, timestamped log of every sync action, at three levels:

| Level   | Message format                                        | Meaning |
|---------|--------------------------------------------------------|---------|
| Info    | `{repo}: found {n} open PR(s)`                          | `gh pr list` succeeded. |
| Info    | `{repo} #{n}: generating briefing`                       | About to call the agent CLI for this PR. |
| Info    | `{repo} #{n}: briefing saved`                            | Briefing generated and written to disk. |
| Info    | `{repo} #{n}: merged — removed`                          | PR merged; its briefing was pruned. |
| Warning | `{repo} #{n}: agent invocation failed — {error}`          | The agent CLI ran but returned a failure/non-zero result — see below. |
| Error   | `{repo}: sync failed — {error}`                          | Repo-level failure, before any PR-specific work — usually `gh` auth or the agent CLI not being found at all. |
| Error   | `{repo} #{n}: sync failed — {error}`                     | A single PR's briefing generation failed; the rest of the repo's PRs still proceed. |

The one-line status text under "Sync Now" just mirrors the *latest* log entry — check the full
panel, not just that line, when something looks off.

### Console output

When run from a terminal (`dotnet run --project src/PrThingy.App`), full exception detail —
including stack traces the sync log's one-line message won't show — prints to that console via the
default logging provider. There is currently **no persisted log file**: `%APPDATA%\PrThingy\logs`
exists on disk but nothing writes to it yet, so the terminal is the only place to see full
diagnostic detail today. If you normally launch the app by double-clicking the built exe, re-run it
from a terminal when you need to see this.

## Common problems

| Symptom | Cause | Fix |
|---|---|---|
| `'gh pr list' failed for repository 'X': ... gh auth login` | `gh` isn't authenticated | `gh auth login`, or set `GH_TOKEN` |
| `Win32Exception (2): ... cannot find the file specified` for `gh`/`claude`/`gemini` | Binary isn't on the *persisted* PATH the app's process sees | See "Windows: the PATH gotcha" above |
| Sync log: `agent invocation failed — {error}` | The CLI ran but exited non-zero — e.g. not logged into the agent itself, rate-limited, or a malformed prompt | Run the same CLI directly from a terminal to reproduce and read its own error output |
| Startup banner won't go away after fixing something | It's only evaluated once, at launch | Restart the app |
| A repo never produces briefings | Local clone missing an `origin` remote, or `gh` can't see PRs from that directory | `git remote -v` in the clone; run `gh pr list` manually from inside it |
