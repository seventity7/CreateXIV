# 🛠️ CreateXIV

CreateXIV is a Dalamud plugin that allows players to create custom slash command aliases with ease.

It combines the flexibility of macros with the power of plugin commands, all through a clean in-game interface.

---

## ✨ Features

- 🔹 Create custom **slash command aliases**
- 🔹 Support for:
  - Macro-based aliases (`macro:##`, `shared:##`)
  - Plugin commands (e.g. `/lifestream`, `/something`)
- 🔹 Fully in-game UI (`/create`)
- 🔹 Automatic alias saving and persistence
- 🔹 Overwrite existing aliases safely
- 🔹 Case-insensitive alias handling
- 🔹 Clean and simple UI design
- 🔹 Tooltip help system built-in

---

## 📦 Installation

### 🔧 Custom Repository Installation

1. Open Dalamud settings
2. Navigate to **Experimental → Custom Plugin Repositories**
3. Add: `https://raw.githubusercontent.com/seventity7/CreateXIV/main/repo.json`
4. Open plugin installer
5. Search for **CreateXIV**
6. Install

---

## 🧠 How It Works

CreateXIV allows you to define:

Alias → Command
Example: Alias: `Golem` - Command: `/lifestream Dynamis Golem`
Type `/Golem` in chat and `/lifestream Dynamis Golem` will be executed.

Alias → Macro
Example: Alias: `Golem` - Macro: `macro:45`
Type `/Golem` in chat and `Macro 45` will be executed 
This will execute a macro from your in-game macro list.

⚠️ Notes:
- The macro must exist in-game
- Macro content is managed inside FFXIV, not the plugin

---

## ⚠️ Important Rules

- ❌ Do NOT use `/` in Alias names  
- ✅ ALWAYS use `/` in Command field for plugin commands  
- ❌ You cannot create an alias named `create`  
- ❌ You cannot point a command to `/create`  
- ⚠️ Native FFXIV commands should be used via **Macro mode**  
- 🔁 Duplicate alias names will overwrite existing ones

---

## 📌 Known Limitations

- Native game commands require execution through macro alias
- Alias execution depends on Dalamud command system (Plugins commands only)

---

## 🚀 Future Plans

- 🔹 UI Themes
- 🔹 More alias categories
- 🔹 Import/export system
- 🔹 Auto-complete suggestions
- 🔹 UI animations
- 🔹 Support for native game commands without macro

---

## 👤 Author

**Bryer**

---

## 💬 Support

If you encounter issues or have suggestions:

- Open an issue on GitHub
- Or contact via Dalamud community

---

## 📜 License

This project is provided as-is for personal use within FFXIV.
