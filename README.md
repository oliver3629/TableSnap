# TableSnap (Windows Starter Project)

Capture a table from anywhere on screen, reconstruct its rows, columns, and merged cells, correct the result, then copy it into Word or Excel or export an `.xlsx` file. Recognition is connected through `ITableRecognizer`; the starter project includes an OpenAI Responses API implementation. The demo table works without an API key.

## Run in VS Code

1. Install the [.NET 10 SDK for Windows](https://dotnet.microsoft.com/download/dotnet/10.0). The runtime alone is not enough to build the project.
2. Install [VS Code](https://code.visualstudio.com/) and Microsoft's **C#** extension. C# Dev Kit is optional.
3. Open the `TableSnap` folder in VS Code.
4. Run these commands in the VS Code terminal:

   ```powershell
   dotnet --version
   dotnet restore
   dotnet run
   ```

You can also press `F5`; the project includes `.vscode/tasks.json` and `.vscode/launch.json`.

## Enable image recognition

Set the API key in the same PowerShell terminal that runs the app:

```powershell
$env:OPENAI_API_KEY = "your-api-key"
dotnet run
```

Do not put the key in source files or commit it to Git. `TABLESNAP_MODEL` can override the model name; the default is `gpt-4o`. Captured images are sent to the configured API. The request uses image input and JSON Schema structured output; see the [OpenAI image input guide](https://developers.openai.com/api/docs/guides/images-vision) and [structured output guide](https://developers.openai.com/api/docs/guides/structured-outputs).

## Use the app

- Press `Ctrl+Shift+2` or click **Capture Region** to select a visible table. Press `Esc` to cancel.
- Click **Open Image** to recognize an existing screenshot.
- Click **Demo Table** to test editing and merged-cell export without an API key.
- Edit the recognized cell text directly in the table preview.
- Click **Copy Table** to place HTML table, TSV text, and CSV data on the clipboard. Paste into Word or Excel.
- Click **Export XLSX** to create an editable workbook. It preserves the grid, row and column merges, bold text, and alignment, and adds uniform basic borders.
- Closing the main window leaves the app in the system tray. Right-click the tray icon to exit.

## Current scope

This starter project preserves table structure and selected basic styles. It does not reconstruct exact fonts, background colors, column widths, complex borders, or numeric cell types. Exported cells are stored as text so that values such as `0012`, percentages, and thousands separators are not silently changed by Excel. Review recognition results before using them. Test capture coordinates on mixed-DPI multi-monitor setups.

The project has no external NuGet dependencies. `Models/TableDocument.cs` defines the internal table structure, `Recognition/ITableRecognizer.cs` allows other recognition engines, and `Export/XlsxExporter.cs` writes an Office Open XML workbook directly.
