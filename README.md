<div align="center">

# Lidarr.Plugin.YandexMusic

**Yandex.Music indexer + download client for [Lidarr](https://lidarr.audio)**

Search Yandex.Music, download encrypted streams, decrypt AES-CTR on the fly, tag with MusicBrainz IDs, hand off to Lidarr's importer.

[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg?style=flat-square)](https://www.gnu.org/licenses/gpl-3.0)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![Lidarr plugins](https://img.shields.io/badge/Lidarr-plugins%20branch-009688?style=flat-square)](https://github.com/Lidarr/Lidarr/tree/plugins)
[![CI](https://img.shields.io/github/actions/workflow/status/kitsunoff/yandex-music-lidarr/ci.yml?branch=main&label=CI&style=flat-square)](../../actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/kitsunoff/yandex-music-lidarr?style=flat-square&color=brightgreen)](../../releases/latest)
[![Lossless](https://img.shields.io/badge/FLAC-24--bit%20%2F%2048kHz-ff6600?style=flat-square)](#what-yandex-actually-serves)

</div>

---

> [!IMPORTANT]
> Lidarr plugin support lives only on the `plugins` / `nightly` / `pr-plugins`
> branch. The stable channel won't load this plugin.

> [!WARNING]
> Lossless FLAC requires an active **Yandex Plus** subscription. Without it
> the API silently downgrades requests to AAC and the download client logs
> a warning every time it happens.

## Table of contents

- [What you get](#what-you-get)
- [What Yandex actually serves](#what-yandex-actually-serves)
- [Install](#install)
- [Configure](#configure)
- [Build from source](#build-from-source)
- [Local Lidarr for end-to-end testing](#local-lidarr-for-end-to-end-testing)
- [Architecture](#architecture)
- [Known limitations](#known-limitations)
- [Credits](#credits)
- [License](#license)

## What you get

| Stage | What the plugin does |
| --- | --- |
| **Indexer** | Calls `/search?type=album` / `?type=artist` on `api.music.yandex.net`, returns one `ReleaseInfo` per quality tier so Lidarr's quality profile can pick. |
| **Download client** | Background queue, parallel track downloads, atomic file writes, MusicBrainz-tagged audio out of the box. |
| **Crypto** | Streaming AES-CTR decryption of the `encraw` transport (no buffering of multi-hundred-MB FLAC blobs in memory). |
| **Auth** | One long-lived OAuth bearer token, the same flow `yandex-music-downloader` uses. No browser cookie wrangling. |
| **Format** | Real FLAC (`flac-mp4` container - FLAC stream inside MP4), up to 24-bit / 48 kHz Hi-Res for Plus accounts. |

## What Yandex actually serves

`ffprobe` confirms the plugin really does pull lossless when Plus is active:

```text
01 - Автомат со льдом.m4a
  Stream #0:0  Audio: flac (fLaC), 44100 Hz, stereo, s16, 965 kb/s    # CD-quality FLAC

01 - выбирал.m4a
  Stream #0:0  Audio: flac (fLaC), 48000 Hz, stereo, s32 (24 bit), 1754 kb/s    # Hi-Res FLAC
```

Files land as `.m4a` because Yandex ships FLAC inside an MP4 container - same trick Apple uses. The audio stream itself is bit-exact FLAC; any decent player or `ffmpeg` extracts it losslessly.

## Install

In Lidarr (on the plugins branch):

1. **Settings → Plugins → Install**
2. Paste the GitHub URL:
   ```text
   https://github.com/kitsunoff/yandex-music-lidarr
   ```
3. **Restart Lidarr.**

Lidarr's `PluginService` picks the latest release whose asset name ends in
`net8.0.zip` and whose body advertises a satisfied `MinimumLidarrVersion`.

## Configure

### Step 1 — Get an OAuth token

Open this URL in a browser, log in with the account that holds your Plus
subscription, and copy the `access_token` fragment out of the redirect URL:

```text
https://oauth.yandex.ru/authorize?response_type=token&client_id=23cabbbdc6cd418abb4b39c32c41195d
```

The redirect lands on something like:

```text
https://music.yandex.ru/#access_token=AQAAAA...&token_type=bearer&expires_in=31535645
```

The value between `=` and `&` is the token. Good for ~1 year, refreshable through the same flow.

### Step 2 — Add the indexer

`Settings → Indexers → Add → Other → Yandex.Music`

| Field | Value |
| --- | --- |
| OAuth Token | paste the token from step 1 |
| Preferred Quality | `Lossless (FLAC)` if you have Plus, otherwise `Normal (AAC 192)` |
| Early Download Limit | optional |

Hit **Test** → should be green. **Save.**

### Step 3 — Add the download client

`Settings → Download Clients → Add → Other → Yandex.Music`

| Field | Value |
| --- | --- |
| OAuth Token | same as on the indexer |
| Download Path | somewhere Lidarr can write (e.g. `/downloads`) |
| Quality | matches the indexer setting |
| Concurrent Tracks | 2 (raise if you hit no rate limits) |
| Embed Lyrics / Cover Resolution | up to taste |

**Test → Save.**

### Step 4 — Enable the protocol in Delay Profiles

> [!CAUTION]
> **This is the step that bites everyone.** Lidarr's `ProtocolSpecification`
> checks the *Delay Profile*, not the download client, when deciding whether
> a release is allowed.

`Settings → Profiles → Delay Profiles → Default → Edit`:

- check **YandexMusic** as Allowed
- drag it to the top if you want it preferred over Usenet / Torrent

Without this every Yandex release gets rejected at decision time with `YandexMusicDownloadProtocol is not enabled for this artist`, and you stare at red ⊙ icons in Manual Search wondering what you did wrong.

### Step 5 — Quality profile

`Settings → Profiles → Quality Profiles → <your profile>`:

- enable `FLAC` (and optionally `AAC 192` / `AAC 64`) in the allowed qualities
- otherwise Lidarr's quality filter rejects everything the indexer surfaces

## Build from source

```bash
git clone --recurse-submodules https://github.com/kitsunoff/yandex-music-lidarr
cd yandex-music-lidarr

make build       # _plugins/net8.0/Lidarr.Plugin.YandexMusic/Lidarr.Plugin.YandexMusic.dll
make test        # YandexMusicSharp unit tests
make package     # Lidarr.Plugin.YandexMusic.net8.0.zip
```

**Prereqs:**

- .NET 8 SDK — `brew install dotnet@8` on macOS, the keg-only formula lines up with the upstream pin in `src/global.json`
- GNU `make`
- `zip` (any recent version)

The `Makefile` passes `--property:NuGetAudit=false --property:AssemblyVersion=1.0.0`
on every `dotnet` invocation. Both are **mandatory**:

| Flag | Why |
| --- | --- |
| `NuGetAudit=false` | Lidarr.Core's `MailKit` triggers NU1902 (known moderate vulnerability) and its own `TreatWarningsAsErrors=true` turns the warning into a hard error during transitive restore. We can't override the submodule's props without modifying it. |
| `AssemblyVersion=1.0.0` | Hotio's release pipeline pins Lidarr.Core to `AssemblyVersion=1.0.0`. The upstream `Directory.Build.props` uses a timestamp wildcard, so without this pin every local build produces a plugin whose TypeRefs point at a one-shot version no Lidarr host actually exposes. Plugin loader silently refuses to load. |

## Local Lidarr for end-to-end testing

A `compose.yaml` spins up `ghcr.io/hotio/lidarr:pr-plugins` with the merged
plugin DLL bind-mounted into `/config/plugins/kitsunoff/Lidarr.Plugin.YandexMusic/`:

```bash
make lidarr-up        # build + start container
make lidarr-logs      # tail Lidarr logs
make lidarr-restart   # rebuild + restart to pick up code changes
make lidarr-reset     # wipe volumes and start fresh
```

Web UI: `http://localhost:18686` (override via `LIDARR_PORT=...`).

## Architecture

```text
.
├── src/
│   ├── YandexMusicSharp/                    # standalone .NET 8 API client
│   │   ├── Auth/RequestSigner.cs            # HMAC-SHA256 sign (ymd recipe)
│   │   ├── Codec/AesCtrDecryptor.cs         # one-shot AES-CTR
│   │   ├── Codec/AesCtrStream.cs            # streaming AES-CTR
│   │   ├── Api/                             # Search / Albums / Artists / DownloadInfo
│   │   ├── Http/YandexMusicHttpClient.cs    # bearer + X-Yandex-Music-Client + envelope
│   │   ├── Models/                          # System.Text.Json DTOs
│   │   └── YandexMusicClient.cs             # façade + decrypted-stream download
│   ├── YandexMusicSharp.Tests/              # NUnit + reference HMAC / AES vectors
│   └── Lidarr.Plugin.YandexMusic/
│       ├── Plugin.cs                        # discovery entry point
│       ├── YandexMusicApi.cs                # per-ALC client cache
│       ├── Indexers/YandexMusic/            # HttpIndexerBase wiring
│       └── Download/Clients/YandexMusic/    # DownloadClientBase + background queue
├── ext/Lidarr/                              # submodule → Lidarr@plugins
├── compose.yaml                             # local Lidarr nightly
├── Makefile                                 # dotnet + docker compose wrappers
└── .github/workflows/{ci,release}.yml       # build, test, release
```

The signing scheme, encryption flow, and quality-tier mapping live in
[`docs/yandex-music-downloader-research.md`](docs/yandex-music-downloader-research.md). Plugin host integration points are catalogued in
[`docs/lidarr-plugin-development-guide.md`](docs/lidarr-plugin-development-guide.md).

## Known limitations

| Limitation | Why |
| --- | --- |
| **MusicBrainz mapping** | Yandex.Music does not expose MBIDs. The indexer searches by `artist + album` text. Albums on Yandex but not on MusicBrainz cannot be imported (Lidarr-side constraint, not a plugin one). |
| **Zero `size`/`bitrate` for FLAC** | Yandex's get-file-info leaves both fields empty for `flac-mp4`. The queue recomputes total from track durations × nominal bitrate. ETA can be ±20% until the first track finishes. |
| **Lossless requires Plus** | Without Plus, Yandex silently downgrades to AAC. The client logs `requested lossless but server returned aac` whenever this happens. |
| **No release-level RSS feed** | Streaming services don't have one. RSS / "recent releases" sync is a no-op; manual or scheduled artist searches are the only triggers. |

## Credits

- [`llistochek/yandex-music-downloader`](https://github.com/llistochek/yandex-music-downloader) — reference Python implementation; the signing recipe and the AES-CTR / zero-nonce decryption procedure are direct ports
- [`MarshalX/yandex-music-api`](https://github.com/MarshalX/yandex-music-api) — upstream JSON shapes and the well-known public client id
- [`TrevTV/Lidarr.Plugin.Tidal`](https://github.com/TrevTV/Lidarr.Plugin.Tidal) & [`TrevTV/Lidarr.Plugin.Deezer`](https://github.com/TrevTV/Lidarr.Plugin.Deezer) — the indexer / download-client wiring pattern, ILRepack setup, `MinimumLidarrVersion` convention
- [@keltecc](https://github.com/llistochek/yandex-music-downloader/issues/112#issuecomment-2812535100) — the `encraw` transport / AES-CTR decryption discovery

## License

[GPLv3](https://www.gnu.org/licenses/gpl-3.0) — same as Lidarr itself, since the plugin links against `Lidarr.Core`.
