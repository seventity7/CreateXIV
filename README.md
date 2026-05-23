`Most recent update: 1.0.6.6 - May 11 2026`

# 🛠️ CreateXIV

CreateXIV is a Dalamud plugin that allows players to create custom slash command aliases with ease.

It combines the flexibility of macros with the power of plugin commands, all through a clean in-game interface.

---

## ✨ Features

- 🔹 Create custom **slash command aliases**
- 🔹 Support for:
  - Macro-based aliases (`macro:##`, `shared:##`)
  - Plugin commands (e.g. `/teste`, `/something`)
  - Game commands (e.g. `/bread`, `/p Message`)
- 🔹 Fully in-game UI (`/create`)
- 🔹 Automatic alias saving and persistence
- 🔹 Overwrite existing aliases safely
- 🔹 Case-insensitive alias handling
- 🔹 Clean and simple UI design
- 🔹 Tooltip help system built-in
- 🔹 Favorites system
- 🔹 Import/Export system
- 🔹 Search + filters
- 🔹 Categories system

---

## 📦 Installation

### 🔧 Oficial Dalamud Installation

1. Open Dalamud plugin list `/xlplugins`
2. Search for `CreateXIV`
3. Click **install**
4. Enable the plugin and open the main window or use `/create`

---

## 🧠 How It Works

### Main window
<img width="1101" height="616" alt="c1" src="https://github.com/user-attachments/assets/0788c7b0-3a09-4008-a2a8-e642c8f10391" />


CreateXIV allows you to define:

Alias → Command
Example: Alias: `Golem` - Command: `/Example Dynamis Golem`
Type `/Golem` in chat and `/Example Dynamis Golem` will be executed.
<img width="1025" height="100" alt="c2" src="https://github.com/user-attachments/assets/cc17257a-23fc-41ea-b2e2-a2ebb9cdb0b9" />


Alias → Macro
Example: Alias: `Golem` - Macro: `macro:45`
Type `/Golem` in chat and `Macro 45` will be executed 
This will execute a macro from your in-game macro list.
<img width="1014" height="94" alt="c3" src="https://github.com/user-attachments/assets/54cccaee-e9f0-4e73-82e3-87ed707cc43f" />
Example Multiple Macros: Alias: `Golem` - Macro: `macro:45`
Type `/Golem` in chat and `Macro 45` will be executed.
<img width="1008" height="92" alt="c4" src="https://github.com/user-attachments/assets/c21b585d-bc08-4fc0-a55e-e522f37072cc" />

While creating commands/macros alias, you can create_(or use existing)_ categories for them with it own color.
You can also Pin/Favorite it to make it appear always on top of the list._(Favorite while creating or through the list itself)_
<img width="1091" height="114" alt="c5" src="https://github.com/user-attachments/assets/27f4a42c-dac0-4633-b6e0-386e2d51f1b0" />


⚠️ Notes:
- The macro must exist in-game
- Macro content is managed inside FFXIV, not the plugin

---

## ⚠️ Important Rules

- ❌ Do NOT use `/` in Alias names  
- ✅ ALWAYS use `/` in Command field for plugin commands  
- ❌ You cannot create an alias named `create`  
- ❌ You cannot point a command to `/create`  
- 🔁 Duplicate alias names will overwrite existing ones

---

## 🚀 Future Plans

- 🔹 UI Themes
- 🔹 More alias categories
- 🔹 Auto-complete suggestions
- 🔹 UI animations

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
