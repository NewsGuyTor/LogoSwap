# LogoSwap

**Custom logo replacement plugin for Jellyfin 10.11+**

Replace the default Jellyfin branding with your own logo across the entire interface—no manual file editing required.

![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11+-00a4dc?style=flat-square&logo=jellyfin)
![.NET](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

---

## Features

- **Simple Upload Interface** — Upload your logo directly from the Jellyfin dashboard
- **One-Click Activation** — Automatically injects CSS & JavaScript into branding settings
- **Live Preview** — See your current logo before applying changes
- **Easy Removal** — Restore default Jellyfin branding with a single click

---

## Installation

### Via Plugin Repository (Recommended)

1. Open Jellyfin Dashboard → **Plugins** → **Repositories**
2. Click **+** to add a new repository:
   - **Name:** `LogoSwap`
   - **URL:** `https://raw.githubusercontent.com/NewsGuyTor/LogoSwap/main/manifest.json`
3. Go to **Plugins** → **Catalog**
4. Find **LogoSwap** and click **Install**
5. Restart Jellyfin

### Manual Installation

1. Download the latest release from [Releases](https://github.com/NewsGuyTor/LogoSwap/releases)
2. Extract `LogoSwap.dll` to your Jellyfin plugins directory:
   - Linux: `/var/lib/jellyfin/plugins/LogoSwap/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\LogoSwap\`
   - Docker: `/config/plugins/LogoSwap/`
3. Restart Jellyfin

---

## Usage

### 1. Upload Your Logo

Navigate to **Dashboard** → **Plugins** → **LogoSwap**

- Click **Select Logo Image (PNG)** and choose your logo file
- Click **Upload Logo**

> **Tip:** For best results, use a PNG with a transparent background. Recommended dimensions: 400×100px or similar wide aspect ratio.

### 2. Apply to Branding

- Click **Apply Custom Logo to Branding**
- Hard refresh your browser (`Ctrl+Shift+R` / `Cmd+Shift+R`)

Your custom logo will now appear across all Jellyfin pages.

### 3. Manage Your Logo

- **Preview** — View your current uploaded logo
- **Delete Logo** — Remove your logo and restore default branding
- **Remove from Branding** — Disable the logo without deleting the file

---

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/logoswap/upload` | POST | Upload a new logo (multipart/form-data) |
| `/logoswap/image` | GET | Retrieve the current logo |
| `/logoswap/delete` | DELETE | Delete the uploaded logo |
| `/logoswap/status` | GET | Check if a logo is configured |
| `/logoswap/script` | GET | Get the injection JavaScript |

---

## How It Works

LogoSwap uses Jellyfin's built-in branding customization system. When you click "Apply Custom Logo to Branding", the plugin:

1. Generates CSS rules that override logo background-images
2. Creates JavaScript to replace logo `<img>` elements
3. Injects both into Jellyfin's Custom CSS & Custom JS settings

This approach is non-destructive—your original Jellyfin files are never modified.

---

## Building from Source

```bash
git clone https://github.com/NewsGuyTor/LogoSwap.git
cd LogoSwap
dotnet build
```

Output: `bin/Debug/net9.0/LogoSwap.dll`

---

## Requirements

- Jellyfin Server 10.11.0+
- .NET 9.0 Runtime (bundled with Jellyfin 10.11)

---

## Troubleshooting

**Logo not appearing after applying?**
- Hard refresh your browser (`Ctrl+Shift+R`)
- Clear browser cache
- Check Dashboard → General → Branding to verify injection is present

**Upload fails?**
- Ensure the file is PNG format
- Check Jellyfin has write permissions to its plugin config directory
- Review server logs for detailed error messages

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

**Author:** [NewsGuyTor](https://github.com/NewsGuyTor)
