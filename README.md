# BrowserWitch

A Windows utility that acts as the default browser and routes URLs to different browsers based on configurable domain rules.

## Features

- **Domain-based routing** - Open Azure links in Edge, GitHub in Chrome, everything else in Firefox (or any combination you like)
- **URL unwrapping** - Resolves Teams safelinks, Outlook protection links, and URL shorteners (bit.ly, t.co, etc.) to the actual destination before routing
- **Tracking removal** - Strips tracking parameters like `fbclid`, `utm_*`, `gclid`, `msclkid`, and more
- **URL simplification** - Cleans up Amazon product URLs to just `/dp/<ASIN>`, YouTube links to just the video ID
- **System tray app** - Runs quietly in the background with a context menu for config, registration, and recent URL log
- **Clipboard routing** - Copy a URL and press `Ctrl+Shift+B` to route it through BrowserWitch from any app
- **Auto-detection** - Discovers installed browsers from the Windows registry on first run

## Getting Started

### Prerequisites

- Windows 10/11
- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build

```
dotnet build
```

### Run

```
dotnet run
```

This starts the system tray application. Right-click the tray icon to access all options.

### Register as Default Browser

1. Right-click the tray icon → **Register as Browser** (requires admin)
2. Open **Windows Settings → Default Apps** and select **BrowserWitch** as your web browser

## Configuration

On first run, BrowserWitch generates a `browserswitch.json` next to the executable with auto-detected browsers and sensible defaults. Edit it via the tray menu or directly.

```json
{
  "defaultBrowser": "chrome",
  "resolve": [
    { "match": "safelinks.protection.outlook.com", "method": "queryParam", "param": "url" },
    { "match": "statics.teams.cdn.office.net", "method": "queryParam", "param": "url" },
    { "match": "bit.ly", "method": "redirect" },
    { "match": "t.co", "method": "redirect" }
  ],
  "clean": {
    "stripParams": ["fbclid", "gclid", "utm_source", "utm_medium", "utm_campaign", "..."],
    "simplify": [
      { "match": "*.youtube.com", "pattern": "/watch\\?v=([a-zA-Z0-9_-]{11}).*", "replace": "/watch?v=$1" },
      { "match": "*.amazon.*", "pattern": ".*/dp/([A-Z0-9]{10}).*", "replace": "/dp/$1" }
    ]
  },
  "rules": [
    { "match": "*.azure.com", "browser": "edge" },
    { "match": "*.sharepoint.com", "browser": "edge" },
    { "match": "*.github.com", "browser": "chrome" }
  ],
  "browsers": {
    "chrome": { "name": "Google Chrome", "path": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe" },
    "firefox": { "name": "Mozilla Firefox", "path": "C:\\Program Files\\Mozilla Firefox\\firefox.exe" },
    "edge": { "name": "Microsoft Edge", "path": "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe" }
  }
}
```

### URL Processing Pipeline

Every URL goes through three stages:

1. **Resolve** - Unwrap safelinks, follow shortener redirects
2. **Clean** - Strip tracking parameters, simplify URLs
3. **Route** - Match domain against rules, open in the right browser

### Domain Matching

Rules use glob patterns matched against the URL's host:

| Pattern | Matches |
|---|---|
| `github.com` | Exactly `github.com` |
| `*.github.com` | `github.com`, `gist.github.com`, `docs.github.com`, etc. |
| `*.amazon.*` | `www.amazon.com`, `www.amazon.de`, `www.amazon.co.uk`, etc. |

Rules are evaluated top to bottom. First match wins. No match falls through to `defaultBrowser`.

### Resolve Methods

| Method | Use Case | How It Works |
|---|---|---|
| `queryParam` | Safelinks, Teams links | Extracts the real URL from a query parameter |
| `redirect` | URL shorteners | Follows HTTP redirects to the final destination |

Resolve rules chain up to 5 levels deep (e.g., safelink → shortener → real URL).

## License

MIT
