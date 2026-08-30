# Known limitations of `0.1.0-rc.1`

- GitHealth only analyses repositories that are already present; it does not clone and
  does not handle forge credentials.
- No `fetch`, `pull` or `remote prune` is ever run. Remote references may be stale until
  the user updates them deliberately.
- The product is local and single-user. It is not designed to be exposed on a LAN, on the
  Internet, or behind a reverse proxy.
- The macOS archives and the `.pkg` installer are neither signed nor notarised. Gatekeeper
  may ask for explicit approval on first launch.
- The Windows installer is not signed. SmartScreen may warn on first launch until a code
  signing certificate is in place.
- In-app updates exist only on Windows and macOS. On Linux, only portable archives are
  published and updating stays manual.
- On Windows, the window needs the WebView2 runtime. When it is missing, the application
  tries to download it and may stop without a message if that download fails.
- GitHealth does not bundle Git: a Git installed on the machine is still required.
  `--git-path` points to an executable located outside the `PATH` and the standard
  locations.
- Repositories with several thousand branches can take several minutes. Rendering stays
  paginated, but does not use row virtualisation yet.
- A branch's activity is approximated by the date of its tip commit. Git stores neither
  the intent behind its creation nor the shared history of every checkout.
- After a merge, attributing commits to their branch of origin can become impossible. The
  interface says so explicitly.
- `.mailmap` normalises the identities known to the repository. Without that file, one
  person using several addresses can appear several times.
- GitHealth does not detect open pull requests and does not replace the retention
  policies of GitHub, GitLab or Azure DevOps.
- No delete, merge, checkout or push operation is offered. Recommendations must be
  verified before any action taken outside GitHealth.
- The local protection restricts requests to the session and the origin generated at
  startup. It does not protect against malware running with the same rights as the user.
- Docker only sees the repositories placed under the `/repositories` mount. Host paths
  and container paths are not interchangeable.
- The application interface is available in French only. The documentation is in English,
  so the labels it quotes are translations of what is shown on screen.
