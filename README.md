`Changelog 1.0.4 - 03/29/2026`

## ✨ Added

- 🔗 **Macro chaining** (execute multiple macros in sequence)
  - Supports: `macro:47, macro:48`
  - Works with **shared macros** as well
- ⏳ **Optional delays** in macro sequences:
  - Example: `macro:5, wait:3, macro:7`
- 🧠 More robust macro execution logic:
  - ✅ Waits for macro execution state (prevents running multiple at once)

## 🛠️ Fixed
- 🧯 Prevented macro sequences from firing simultaneously
- 🧷 Reduced risk of macro-trigger race conditions

## 🎨 UI Overhaul + Power Features

- ⭐ **Favorite / Pin system**
  - Toggle with **★ button**
  - Pinned entries appear at the top
- 🗂️ **Categories**
  - Dropdown with typing + autocomplete
  - Auto-creates new categories on save
- 🎨 **Category color system**
  - Choose a color per category (color-hex picker)
  - Entire row adopts category color (N° / Type / Category / Alias / Command)
- 🔎 **Search + Filters**
  - Search by alias/command/category
  - Filter by Type (All / Macro / Command)
  - Filter by Category
- 📦 **Import / Export**
  - Export aliases to clipboard (JSON)
  - Import aliases from clipboard (JSON)
- 🧪 **Test tools**
  - Run any alias instantly without typing in chat
- 🧬 **Duplicate Alias**
  - Quick clone with automatic `_copy` naming
- ↩️ **Undo Delete**
  - Restore last deleted alias

## 📝 Improved
- 🧹 Better live validation during creation (**OK / Invalid**)
- 🔒 Safety rules reinforced:
  - Blocks alias loops (alias ➜ alias)
  - Blocks creating `/create` or pointing to `/create`
- 🧊 Optional command anti-spam cooldown support (config-driven)

## 🎛️ UI/UX
- ❓ **Help tooltips** for both sections:
  - “How to use”, “Examples”, “Usage”
  - Styled to match plugin theme
- 🧼 Removed top title block to reduce clutter
- 🧷 Cleaned list visuals and interaction flow

## 🧾 Notes
- **Macro Alias** supports native game commands through macro execution (ex: `/echo`, `/tell`)  
- **Plugin Command Alias** is intended for Dalamud/plugin commands (requires starting with `/`)  
- Duplicate alias names overwrite/update existing ones by design ✅

`End of changelog`

# 🛠️ CreateXIV

CreateXIV is a Dalamud plugin that allows players to create custom slash command aliases with ease.

It combines the flexibility of macros with the power of plugin commands, all through a clean in-game interface.

---

## ✨ Features

🔹 Create custom **slash command aliases**
🔹 Support for:
  - Macro-based aliases (`macro:##`, `shared:##`)
  - Plugin commands (e.g. `/lifestream`, `/something`)
🔹 Fully in-game UI (`/create`)
🔹 Automatic alias saving and persistence
🔹 Overwrite existing aliases safely
🔹 Case-insensitive alias handling
🔹 Clean and simple UI design
🔹 Tooltip help system built-in
🔹 Favorites system
🔹 Import/Export system
🔹 Search + filters
🔹 Categories system
🔹 Support to run multiple macros in sequence

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
