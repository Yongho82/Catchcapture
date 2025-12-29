# Note Settings and Format Information Implementation

## Changes Made

### 1. Settings Model Update (`Settings.cs`)
- Added properties for Note File Name Template (`NoteFileNameTemplate`) and Grouping (`NoteFolderGroupingMode`).
- Changed default `NoteSaveFormat` to `WEBP`.

### 2. User Interface Updates (`SettingsWindow.xaml`)
- **Capture Settings**:
  - Automatically added an "Info" button (`?`) next to the Image Quality setting.
  - Clicking this shows a popup with detailed format information (PNG, JPG, BMP, GIF, WEBP).
  
- **Note Settings**:
  - Added an "Info" button (`?`) next to the Note Image Quality setting.
  - Added a new **File Naming** section:
    - Template Input (supports placeholders like `$yyyy$`, `$App$`, `$Title$`).
    - Presets Dropdown (Default, Simple, Timestamp).
    - Live Preview of the file name.
  - Added a new **Folder Grouping** section:
    - Options: None, Monthly, Quarterly, Yearly.

### 3. Logic Implementation (`SettingsWindow.xaml.cs`)
- Implemented handlers for loading and saving the new Note settings.
- Added logic for the "Info" popup toggle.
- Implemented real-time preview generation for note filenames based on the template and grouping selection.

### 4. Backend Integration (`NoteInputWindow.xaml.cs`)
- Updated the **Note Saving** logic (`BtnSave_Click`) to respect the user's settings:
  - **File Naming**: Now generates file names using the defined template.
  - **Folder Grouping**: Now creates and saves images into subfolders (e.g., `2025-01` or `2025_1Q`) based on the grouping setting.
  - **Collision Handling**: Automatically appends counters (e.g., `_1`, `_2`) if a file with the generated name already exists.

## Verification
- **Build**: Successful.
- **Functionality**:
  - The format info popup appears correctly for both sections.
  - Note filename templates are saved and applied to new captures/notes.
  - Note folder grouping is applied to the storage structure.

## Next Steps needed from User
- **Localization**:
  - The following resource keys were used and should be added to `Strings.ko.resx` (although hardcoded fallbacks are provided, so it works immediately):
    - `FileNamePreset_Default`
    - `FileNamePreset_Simple`
    - `FileNamePreset_Timestamp`
