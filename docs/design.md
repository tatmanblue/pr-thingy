# Goal

Create a GUI desktop application that will run on Windows, MacOS and linux.  It is written in C#.

Instead of forcing you to hunt down code changes, click through multiple GitHub tabs, or manually run terminal commands when you're ready to work, it completely flips the workflow. It shifts your code review process from reactive pulling to proactive preparation.

Here is the best way to explain what this system does for you:

An Invisible Assistant: It acts like a silent engineering assistant that works while you are away. It constantly monitors your team's repository, pulls down the latest code updates, and reads the intent behind the pull requests.

A Static Intelligence Layer: It uses your local computer to securely talk to GitHub, meaning you get all the benefits of automation without requiring admin access, changing team settings, or exposing private tools to the shared codebase.

Pre-Baked Context: When you open the dashboard app, you aren't just looking at a list of PR numbers. You are looking at structured, plain-English briefings. Before you even open your web browser, you already know the why behind the code, exactly which high-impact files changed, and the top 3 technical risks you need to watch out for.

# Additional files
[plan1.md](plan1.md) -- contains some of the original ideas before considering making a GUI application.

# Directory Structure
* solution file belongs in the root directory  
* all projects belong under the src directory  

# Assumptions
* user will install github cli tools needed
* can work with any agent -- so lets keep the verbage generic, if we need specifics for interacting with different agents, the specifics are resolved via an interface/DI.  Focus on gemini and claude for now.

