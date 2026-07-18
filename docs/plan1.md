# Automated Local PR Review Agent (Pre-Review Dashboard Setup)

This guide shows you how to build a **fully automated, local background worker** that pre-summarizes your team's GitHub Pull Requests. It runs silently in the background on your computer—requiring **no repository access modifications**, **no team actions**, and **zero daily manual steps**. 

When you sit down to work, your summaries are already written and waiting for you in a local folder (`~/PR_Summaries/`).

---

## 🏗️ How It Works

```
                     [ Your Computer (Mac/Linux/WSL) ]
                                    │
    ┌───────────────────────────────┴───────────────────────────────┐
    ▼                                                               ▼
[ Background Scheduler ]                                    [ Summary Dashboard ]
Runs silently every hour                                     Your local folder (~/PR_Summaries/)
    │                                                               │
    ├─► 1. Fetches open team PRs via `gh`                           ├─► PR_104_summary.md (Read)
    ├─► 2. Grabs code diff & description                            ├─► PR_105_summary.md (Read)
    ├─► 3. Feeds context to Gemini CLI                              └─► *Already prepared before you look!*
    └─► 4. Saves Markdown summary to folder
```

---

## 🛠️ Step 1: Install Dependencies & Authenticate

Your local agent needs two command-line tools to work: the **GitHub CLI** (to securely fetch PR data) and the **Gemini CLI** (to write the summaries).

### 1. Install GitHub CLI (`gh`)
* **macOS:** `brew install gh`
* **Windows (Git Bash/WSL) / Linux:** Install from [cli.github.com](https://cli.github.com)

Once installed, authenticate your account by running:
```bash
gh auth login
```
*Follow the interactive prompts to log in via your browser. Once authenticated, the background worker can securely pull data without asking for credentials.*

### 2. Install & Configure Gemini CLI
Install the Gemini CLI tool and make sure your Gemini API Key is exported in your environment profile (e.g., `~/.zshrc`, `~/.bashrc`, or `~/.profile`):
```bash
export GEMINI_API_KEY="your_actual_gemini_api_key_here"
```

---

## 📝 Step 2: Create the Background Worker Script

Create a script named `sync_reviews.sh` in your home directory. This script fetches open PRs, checks if they have already been summarized, and generates summaries for any new ones.

1. Create the file:
   ```bash
   nano ~/sync_reviews.sh
   ```
2. Paste the following script:
   ```bash
   #!/bin/bash

   # --- CONFIGURATION ---
   # Update this to the absolute path of your local team repository clone
   REPO_DIR="$HOME/projects/your-team-repo"
   # Directory where your finalized summaries will be stored
   OUTPUT_DIR="$HOME/PR_Summaries"
   # ---------------------

   # Ensure we can find 'gh' and 'gemini' when running via scheduler
   export PATH="/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:$PATH"
   # If using Homebrew on macOS (Apple Silicon), include its path:
   export PATH="/opt/homebrew/bin:$PATH"

   # Ensure API key is present (set your key below if not in system environment)
   # export GEMINI_API_KEY="your_api_key_here"

   cd "$REPO_DIR" || { echo "Directory not found"; exit 1; }

   # Fetch latest state from GitHub silently
   git fetch origin > /dev/null 2>&1

   mkdir -p "$OUTPUT_DIR"

   # Fetch the 10 most recent open PR numbers
   PR_LIST=$(gh pr list --limit 10 --json number --jq '.[].number')

   for PR in $PR_LIST; do
     # Skip if summary already exists to save time and API tokens
     if [ -f "$OUTPUT_DIR/PR_${PR}_summary.md" ]; then
       continue
     fi

     echo "Agent: Generating summary for PR #$PR..."

     # Extract title, body text, and code modifications
     PR_INFO=$(gh pr view "$PR" --json title,body --jq '"Title: " + .title + "\n\nDescription:\n" + .body')
     PR_DIFF=$(gh pr diff "$PR")

     # Feed everything to the Gemini CLI
     (
       echo "$PR_INFO"
       echo -e "\n--- CODE CHANGES ---"
       echo "$PR_DIFF"
     ) | gemini run --instructions "You are my personal code-review prep assistant. Summarize this PR's objective, list key files changed, and bullet-point 3 specific risks/bugs/edge cases I should look out for during my review. Be concise." > "$OUTPUT_DIR/PR_${PR}_summary.md"
   done
   ```
3. Save and close (`Ctrl+O`, `Enter`, `Ctrl+X` in nano).
4. Make the script executable:
   ```bash
   chmod +x ~/sync_reviews.sh
   ```

---

## ⏰ Step 3: Automate It (Choose Your System)

Set your operating system's built-in scheduler to run `sync_reviews.sh` automatically.

### Option A: macOS (Using Launchd)
This is the most reliable way on macOS to ensure it runs even if you restart your machine.

1. Create a definition file in your user agent directory:
   ```bash
   nano ~/Library/LaunchAgents/com.user.pragent.plist
   ```
2. Paste the following XML config (configured to run every **1 hour**):
   ```xml
   <?xml version="1.0" encoding="UTF-8"?>
   <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.2.dtd">
   <plist version="1.0">
   <dict>
       <key>Label</key>
       <string>com.user.pragent</string>
       <key>ProgramArguments</key>
       <array>
           <string>/bin/bash</string>
           <string>/Users/YOUR_USERNAME/sync_reviews.sh</string>
       </array>
       <key>StartInterval</key>
       <integer>3600</integer>
       <key>RunAtLoad</key>
       <true/>
       <key>StandardOutPath</key>
       <string>/tmp/pragent.out</string>
       <key>StandardErrorPath</key>
       <string>/tmp/pragent.err</string>
   </dict>
   </plist>
   ```
   *(Be sure to replace `YOUR_USERNAME` in the script path with your actual macOS username. You can find it by typing `whoami` in terminal.)*

3. Load the agent so it starts running immediately:
   ```bash
   launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.user.pragent.plist
   ```

---

### Option B: Linux or Windows WSL (Using Cron)
If you are on Linux or running within WSL:

1. Open your user crontab editor:
   ```bash
   crontab -e
   ```
2. Append the following line to the end of the file to run the script at the top of every hour:
   ```text
   0 * * * * /bin/bash /home/YOUR_USERNAME/sync_reviews.sh > /dev/null 2>&1
   ```
   *(Be sure to replace `YOUR_USERNAME` with your real username).*

---

## 📂 Step 4: The "Review Dashboard" Experience

Once configured, you never have to launch or trigger the agent again. When you sit down to start reviews:

1. Open your file explorer or favorite editor (such as VS Code, Obsidian, or simply the Terminal) to:
   ```
   ~/PR_Summaries/
   ```
2. You will find files automatically generated like:
   * `PR_124_summary.md`
   * `PR_125_summary.md`
3. Click any file to get an instant, personalized brief of the changes before you even open GitHub in your web browser!
