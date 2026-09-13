# Jellyfin Show Director

Adds a director line to library cards in Jellyfin Web:

```
Movie Title
Jane Smith
2024
```

The director name is centered under the title. It reads the director
straight from Jellyfin's own metadata (the `People` list on each item,
filtered to `Type === "Director"`) — no extra scraping or external API
calls.

## How it works

- **C# server plugin** (`Jellyfin.Plugin.ShowDirector`) does two things:
  1. Serves a config page in the admin dashboard (enable/disable, separator,
     max directors shown, which item types to apply to).
  2. On every server startup, patches `jellyfin-web/index.html` to add one
     `<script>` tag pointing at an embedded JS file the plugin also serves.
     It re-applies this patch on every start, so a Jellyfin **server**
     update (which replaces `index.html`) doesn't silently undo it — you
     just need to restart Jellyfin once after each update for it to be
     re-applied.
- **Client JS** (`Web/show-director.js`) runs in the browser. It uses a
  `MutationObserver` to notice cards as Jellyfin renders/recycles them
  (virtual scrolling), pulls the item's `People` data via `ApiClient`
  (already authenticated in the browser session — no API key needed), and
  inserts a centered line between the title and the year.

This only affects **Jellyfin Web** and clients built on it (desktop
browsers, the desktop app, PWA). It will not affect native Android TV,
Roku, tvOS, Kodi, etc. — those apps don't use this HTML/JS at all.

## Install (recommended: plugin repository)

This repo is set up so Jellyfin can install and update the plugin through
its normal **Plugins → Catalog** UI, instead of you copying the DLL by
hand each time.

1. In Jellyfin, go to **Dashboard → Plugins → Repositories → +**.
2. Add this repository's manifest URL:
   ```
   https://raw.githubusercontent.com/uglygus/jellyfin-plugin-show-director/main/manifest.json
   ```
   (Replace `YOUR_GITHUB_USERNAME`/`YOUR_REPO_NAME` with wherever you've
   pushed this repo — see [Releasing](#releasing-a-new-version-maintainers)
   below if you haven't cut a release yet, since the manifest starts
   empty and only has entries after the first tagged release.)
3. Save, then go to the **Catalog** tab — "Show Director" should now
   appear there. Install it like any other plugin.
4. **Restart Jellyfin.** This is required — the `index.html` patch only
   runs at server startup.
5. Go to **Plugins → Show Director** to confirm it loaded and to adjust
   settings (separator, max directors, which item types get the line). Any
   change here also requires a restart, since it's baked into the injected
   `<script>` tag.
6. Reload Jellyfin Web in your browser (hard refresh, `Ctrl+Shift+R` /
   `Cmd+Shift+R`, to bypass the cached `index.html`).

Future updates then show up as a normal update badge in **Plugins →
My Plugins**, same as any catalog plugin.

## Releasing a new version (maintainers)

This repo builds and publishes itself via GitHub Actions — you don't need
the .NET SDK locally unless you're actively developing.

1. Fork/push this repo to your own GitHub account.
2. Edit `Jellyfin.Plugin.ShowDirector/build.yaml` once and set `owner:`
   to your name (cosmetic — shows in the plugin catalog listing).
3. Commit your changes to `main`.
4. Tag a release and push the tag:
   ```bash
   git tag v1.1.0
   git push origin v1.1.0
   ```
   (Use a plain `major.minor.patch` — the workflow expands it to the
   4-part assembly version Jellyfin expects.)
5. The `.github/workflows/release.yml` workflow will:
   - Build the plugin DLL and package it into a versioned zip with
     [jprm](https://github.com/oddstr13/jellyfin-plugin-repository-manager).
   - Attach that zip to a new GitHub Release matching the tag.
   - Append/update that version's entry in `manifest.json`, pointing at
     the release asset, and commit the updated `manifest.json` back to
     `main`.
6. Once that workflow finishes, the manifest URL above will list the new
   version and Jellyfin will offer it as an update.

Note: the workflow checks out and pushes to `main` directly, so tag
whatever commit is currently the tip of `main`.

## Manual install (no repository, copy the DLL yourself)

If you'd rather not set up the GitHub repository/catalog flow, you can
still build and copy the DLL directly, same as before:

You'll need the [.NET 9 SDK](https://dotnet.microsoft.com/download) on
your machine (this doesn't need to be the Jellyfin server itself — build
anywhere with internet access, then copy the DLL over). Jellyfin 10.11.x
runs on .NET 9 — don't install .NET 10, it's only used by the still-in-beta
Jellyfin 12.0 line.

```bash
cd Jellyfin.Plugin.ShowDirector
dotnet restore
dotnet build -c Release
```

This produces:

```
bin/Release/net9.0/Jellyfin.Plugin.ShowDirector.dll
```

This project is pinned to `Jellyfin.Controller`/`Jellyfin.Model` version
`10.11.6`, matching your server. If you upgrade Jellyfin later, check the
new version in **Dashboard → General**, then update the
`<PackageReference>` versions in `Jellyfin.Plugin.ShowDirector.csproj`
to match and run `dotnet restore` again.

Then:

1. On your Jellyfin server, find (or create) the plugin directory, e.g. for
   Docker installs this is usually a mounted volume like:
   ```
   /config/plugins/ShowDirector/
   ```
   For a bare-metal install it's typically:
   ```
   /var/lib/jellyfin/plugins/ShowDirector/
   ```
2. Copy the built DLL there:
   ```bash
   mkdir -p /config/plugins/ShowDirector
   cp bin/Release/net9.0/Jellyfin.Plugin.ShowDirector.dll /config/plugins/ShowDirector/
   ```
3. Restart Jellyfin, then follow steps 4–6 from the repository install
   section above (config page, restart-on-change, hard refresh).

## Troubleshooting

- **Nothing shows up**: Open browser dev tools → Console, and run
  `localStorage.setItem('ShowDirectorDebug', '1')`, then reload. The
  script logs `[ShowDirector] initialized ...` on load and logs failed
  lookups. If you see nothing at all, the script tag likely wasn't
  injected — view page source and search for `ShowDirector` to confirm.
- **A title has no director listed**: it likely has no `Director` entry in
  its `People` metadata in Jellyfin. Check under the item's details page —
  if Jellyfin itself doesn't show a director, there's nothing for the
  plugin to read. You may need to refresh metadata for that item.
- **After a Jellyfin server update, the director line disappeared**: this
  is expected — the update replaced `index.html`. Just restart Jellyfin
  once and the plugin will re-patch it on startup.
- **Restoring the original index.html**: the plugin saves a one-time
  backup the first time it patches, next to the original file, named
  `index.html.ShowDirector.bak`.

## Uninstall

1. In the admin dashboard, either delete the plugin (Dashboard → Plugins)
   or just toggle **Enabled** off and restart once (this cleanly removes
   the injected script tag from `index.html`).
2. If you deleted the DLL directly without unchecking Enabled first, you
   can manually restore `index.html` from the `.bak` file described above.
