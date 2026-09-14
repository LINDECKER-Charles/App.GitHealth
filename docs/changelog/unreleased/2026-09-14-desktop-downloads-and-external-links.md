# Exports and the guide, working inside the desktop window

- **Type** — `fix`
- **Scope** — `api`, `front`
- **Landed** — 2026-09-14
- **Commits** — `773be21`, `7d0c3ed`

## What shipped

In the desktop window, four buttons that did nothing at all now do what they say:

- **User guide** — the book in the top bar opens the guide in the system browser. So does
  every other external link in the product: the ones the assistant writes in its answers
  and the agent installation links in the assistant panel.
- **Back up the data** — the arrow in the top bar writes the SQLite backup to the
  downloads folder, under the name the API stamps with the moment it was taken, and a toast
  says where it landed.
- **Export as CSV** and **Export the selection** — the snapshot and the ticked rows reach
  the disk the same way. The toast now follows the file: nothing is announced when nothing
  was written.

A file whose name is already taken is numbered — `branches (2).csv` — rather than
overwriting the one already there.

Docker mode and browser mode are untouched: the bridge only exists inside the window, and
with no bridge the anchors behave exactly as before.

## Why

The desktop shell is Photino, which draws the window with WKWebView on macOS and WebKitGTK
on Linux. Reading the shipped `Photino.Native` binary settles what those two can do:

- its `UiDelegate` implements six selectors, and
  `webView:createWebViewWithConfiguration:forNavigationAction:windowFeatures:` is not one
  of them — it only appears as optional `WKUIDelegate` protocol metadata. There is no
  reference to `NSWorkspace` or `openURL` anywhere in the binary either. A
  `target="_blank"` therefore asks WebKit for a window nobody creates, and the click dies
  in silence.
- the `WKDownloadDelegate` protocol is absent, and so is
  `download:decideDestinationUsingResponse:suggestedFilename:completionHandler:`. WebKit
  turns a response it cannot display into a download, finds no delegate to give it a
  destination, and drops it. Every anchor carrying `download` was affected — the SQLite
  backup served by `/api/exports/database` and both CSV exports built in the page.

Windows was spared because WebView2 handles popups and downloads natively, which is why
the bug read as macOS-only.

The existing folder-dialog bridge already had the right shape for this, so it was widened
rather than duplicated: `DesktopFolderBridge` became `DesktopBridge`, a dispatcher over
three handlers — `DesktopFolderPicker`, `DesktopExternalLink`, `DesktopFileSaver` — under
`Hosting/Desktop/Bridge/`. External links go through `SystemBrowserLauncher`, which already
turned away everything that is not http(s). Saved names are refused if they carry a
separator, so no payload from the page can write outside the downloads folder.

Two shapes were rejected. Letting the host fetch the file from a URL the page hands it
would have given the page a way to make the host issue requests, for no gain. Opening
`/api/exports/database` in the system browser so it downloads the backup itself would have
flashed a tab open and leant on the loopback origin being reachable from outside the
window.

The file goes to the downloads folder rather than through a **Save as…** panel because
that is where these buttons already put it in a browser, and because Photino's macOS save
panel sets `directoryURL` but never `nameFieldStringValue` — the user would have had to
type `githealth-backup-20260914-153000.db` by hand.

## Consequences

- **The whole database passes through the page's memory when it is backed up from the
  window.** The bridge carries text, so the bytes travel base64-encoded; the peak is a few
  times the size of the base. Accepted: the base holds branch facts and no repository
  content. A browser still streams it straight to disk.
- **Windows changes behaviour too.** Inside the window, exports now land in the downloads
  folder with a toast instead of going through the WebView2 download bar. One rule holds on
  the three platforms: in the window the host saves, in a browser the browser saves.
