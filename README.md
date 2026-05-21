# Lidarr.Plugin.YandexMusic

Yandex.Music indexer + download client plugin for [Lidarr](https://lidarr.audio).
Searches an account-bound Yandex.Music catalogue, downloads the encrypted
streams the Android client uses, decrypts them on the fly, writes proper
MusicBrainz-tagged audio files into Lidarr's library.

Status: works end-to-end (search, FLAC / AAC download, AES-CTR decrypt,
TagLib# tagging, Lidarr library import). Tested against the
`ghcr.io/hotio/lidarr:pr-plugins` host on Apple Silicon under OrbStack.

## Requirements

| | |
| --- | --- |
| Lidarr | `plugins` branch (nightly / pr-plugins) - the stable channel does not ship plugin support |
| .NET runtime | net8.0 (Lidarr already bundles it) |
| Yandex.Music | Active OAuth token; **Yandex Plus** for the lossless tier |

## Install

In Lidarr (on the plugins branch):

1. `Settings -> Plugins -> Install`
2. Paste the GitHub URL:
   ```text
   https://github.com/kitsunoff/yandex-music-lidarr
   ```
3. Restart Lidarr.

Lidarr's `PluginService` picks the latest release whose asset name ends in
`net8.0.zip` and whose body advertises a satisfied `MinimumLidarrVersion`.

## Configure

### 1. Get an OAuth token

Open this URL in a browser, log in with the account that holds your Plus
subscription, and copy the `access_token` fragment back out of the
redirect URL:

```text
https://oauth.yandex.ru/authorize?response_type=token&client_id=23cabbbdc6cd418abb4b39c32c41195d
```

The redirect lands on something like
`https://music.yandex.ru/#access_token=AQAAAA...&token_type=bearer&expires_in=31535645` -
the value between `=` and `&` is the token. The token is good for about a
year and refreshable through the same flow.

### 2. Add the indexer

`Settings -> Indexers -> Add -> Other -> Yandex.Music`:

- **OAuth Token** - paste the token from step 1
- **Preferred Quality** - `Lossless (FLAC)` if you have Plus, otherwise `Normal (AAC 192)`
- everything else can stay at defaults

Hit **Test** -> should be green. Save.

### 3. Add the download client

`Settings -> Download Clients -> Add -> Other -> Yandex.Music`:

- **OAuth Token** - same token as on the indexer
- **Download Path** - somewhere Lidarr can write (e.g. `/downloads`)
- **Quality** - matches the indexer setting
- **Embed Lyrics**, **Cover Resolution** - up to taste

Hit **Test**, then **Save**.

### 4. Enable the protocol in Delay Profiles

This is the step that bites everyone. Lidarr's `ProtocolSpecification`
checks the *Delay Profile*, not the download client, when deciding whether
a release is allowed.

`Settings -> Profiles -> Delay Profiles -> Default -> Edit`:

- check **YandexMusic** as Allowed
- drag it to the top if you want it preferred over Usenet/Torrent

Without this every Yandex release gets rejected at decision time with
`YandexMusicDownloadProtocol is not enabled for this artist`.

### 5. Quality profile

`Settings -> Profiles -> Quality Profiles -> <your profile>`:

- enable `FLAC` (and optionally `AAC 192` / `AAC 64`) in the allowed
  qualities, otherwise Lidarr's quality filter rejects everything the
  indexer surfaces

## Build from source

```bash
git clone --recurse-submodules https://github.com/kitsunoff/yandex-music-lidarr
cd yandex-music-lidarr
make build       # produces _plugins/net8.0/Lidarr.Plugin.YandexMusic/Lidarr.Plugin.YandexMusic.dll
make test        # runs the YandexMusicSharp unit tests
make package     # makes Lidarr.Plugin.YandexMusic.net8.0.zip
```

Prereqs:

- .NET 8 SDK (`brew install dotnet@8` on macOS - the keg-only formula lines
  up with the upstream pin in `src/global.json`)
- GNU make
- `zip` (any reasonably recent version)

The `Makefile` passes `--property:NuGetAudit=false --property:AssemblyVersion=1.0.0`
on every `dotnet` invocation. Both are mandatory:

- `NuGetAudit=false` keeps Lidarr.Core's `MailKit` NU1902 vulnerability
  warning from tripping its own `TreatWarningsAsErrors`. The submodule is
  consumed by ProjectReference so its props apply during transitive
  restore; we cannot override the warning without modifying the submodule.
- `AssemblyVersion=1.0.0` matches the value Hotio's release pipeline
  publishes for Lidarr.Core. Without it the upstream `Directory.Build.props`
  stamps a timestamp-based wildcard, our plugin's TypeRefs bind to a
  one-shot version no Lidarr host actually exposes, and the plugin loader
  silently refuses to load.

## Run a local Lidarr for end-to-end testing

A `compose.yaml` spins up `ghcr.io/hotio/lidarr:pr-plugins` with the merged
plugin DLL bind-mounted into `/config/plugins/kitsunoff/Lidarr.Plugin.YandexMusic/`.

```bash
make lidarr-up        # build the plugin + start the container
make lidarr-logs      # tail the Lidarr logs
make lidarr-restart   # rebuild + restart to pick up code changes
make lidarr-reset     # wipe volumes and start fresh
```

Web UI lands on `http://localhost:18686` (override via `LIDARR_PORT=...`).

## Architecture

```text
.
├── src/
│   ├── YandexMusicSharp/                    # standalone .NET 8 API client
│   │   ├── Auth/RequestSigner.cs            # HMAC-SHA256 sign (ymd scheme)
│   │   ├── Codec/AesCtrDecryptor.cs         # one-shot AES-CTR
│   │   ├── Codec/AesCtrStream.cs            # streaming AES-CTR
│   │   ├── Api/                             # Search / Albums / Artists / DownloadInfo
│   │   ├── Http/YandexMusicHttpClient.cs    # bearer + X-Yandex-Music-Client + envelope
│   │   ├── Models/                          # System.Text.Json DTOs
│   │   └── YandexMusicClient.cs             # façade + decrypted-stream download
│   ├── YandexMusicSharp.Tests/              # NUnit, runs against the upstream
│   │                                        # HMAC and AES test vectors
│   └── Lidarr.Plugin.YandexMusic/
│       ├── Plugin.cs                        # discovery entry point
│       ├── YandexMusicApi.cs                # per-AssemblyLoadContext client cache
│       ├── Indexers/YandexMusic/            # HttpIndexerBase wiring
│       └── Download/Clients/YandexMusic/    # DownloadClientBase + background queue
│           └── Queue/{DownloadItem,DownloadTaskQueue}.cs
├── ext/Lidarr/                              # submodule -> Lidarr@plugins
├── compose.yaml                             # local Lidarr nightly
├── Makefile                                 # dotnet + docker compose wrappers
└── .github/workflows/{ci,release}.yml       # build, test, release
```

The signing scheme, encryption flow, and quality-tier mapping are documented
in detail under [`docs/yandex-music-downloader-research.md`](docs/yandex-music-downloader-research.md).
Plugin host integration points are catalogued under
[`docs/lidarr-plugin-development-guide.md`](docs/lidarr-plugin-development-guide.md).

## Known limitations

- **MusicBrainz mapping.** Yandex.Music does not expose MBIDs. The indexer
  searches by `artist + album` text, so albums that exist on Yandex but
  not on MusicBrainz cannot be imported by Lidarr (a Lidarr-side
  constraint, not a plugin one).
- **`size`/`bitrate` zero for FLAC.** Yandex's get-file-info response
  leaves both fields empty for the `flac-mp4` codec. The queue
  recomputes a total from track durations + nominal bitrate; ETA
  estimates can be off by ~20 percent until the first track finishes.
- **Lossless requires Plus.** Without an active Plus subscription Yandex
  silently downgrades to AAC. The download client logs a warning
  (`requested lossless but server returned ...`) whenever the served
  codec disagrees with the requested quality.
- **Per-track progress is per-chunk but per-track-aggregated.** The CDN
  body is streamed through AES-CTR in 64 KiB chunks; `DownloadedSize`
  is updated atomically on each chunk. ETA is null for the first track
  (no rate samples yet).

## Credits

- [`llistochek/yandex-music-downloader`](https://github.com/llistochek/yandex-music-downloader) - reference Python implementation; the signing recipe and the AES-CTR/zero-nonce decryption procedure are direct ports.
- [`MarshalX/yandex-music-api`](https://github.com/MarshalX/yandex-music-api) - upstream JSON shapes and the well-known public client id.
- [`TrevTV/Lidarr.Plugin.Tidal`](https://github.com/TrevTV/Lidarr.Plugin.Tidal) and [`TrevTV/Lidarr.Plugin.Deezer`](https://github.com/TrevTV/Lidarr.Plugin.Deezer) - the indexer/download-client wiring pattern, ILRepack setup, and `MinimumLidarrVersion` convention.
- [@keltecc](https://github.com/llistochek/yandex-music-downloader/issues/112#issuecomment-2812535100) - the `encraw` transport / AES-CTR decryption discovery.

## License

GPLv3 - same as Lidarr itself, since the plugin links against `Lidarr.Core`.
