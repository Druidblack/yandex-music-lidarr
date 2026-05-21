# yandex-music-downloader — Technical Research

Reference notes compiled from the source of [llistochek/yandex-music-downloader](https://github.com/llistochek/yandex-music-downloader)
(`main` branch, version `3.5.5` at the time of writing) and its upstream library
[MarshalX/yandex-music-api](https://github.com/MarshalX/yandex-music-api). The goal is to produce a working
understanding for porting the relevant logic into a Lidarr plugin (.NET / C#) that uses Yandex.Music as an
indexer / download client.

## 1. Architecture and Language

### Language and packaging

- **Language:** Python 3.9+ ([`pyproject.toml`](https://github.com/llistochek/yandex-music-downloader/blob/main/pyproject.toml#L12)).
- **Build backend:** `setuptools` + `setuptools-git`.
- **Entry point:** console script `yandex-music-downloader` -> `ymd.cli:main`
  ([`pyproject.toml`](https://github.com/llistochek/yandex-music-downloader/blob/main/pyproject.toml#L21-L23)).
- **Distribution:** installed via `pip install https://github.com/llistochek/yandex-music-downloader/archive/main.zip`.

### Dependencies (`pyproject.toml`)

```toml
dependencies = [
    "yandex-music>=3.0.0",   # MarshalX/yandex-music-api - the actual API client
    "mutagen>=1.47.0",       # ID3 / FLAC / MP4 tag writing
    "StrEnum",               # string-valued enums
    "pycryptodome"           # AES-CTR decryption of downloaded audio
]
```

This project is **a thin orchestrator**: it does not implement its own HTTP layer. All REST calls to
`api.music.yandex.net` are delegated to `yandex-music` (MarshalX). What this project does itself is:

1. Resolve a URL/ID into a stream of `Track` objects.
2. Call a **non-public** signed endpoint `get-file-info` that the upstream library does not yet wrap
   (this is the only direct HTTP call in `ymd`).
3. Download the encrypted bytes and **decrypt with AES-CTR**.
4. Embed metadata via `mutagen`.

### Module layout

```text
ymd/
├── __init__.py        # empty
├── __main__.py        # python -m ymd entrypoint (calls cli.main)
├── api.py             # download-info + decryption logic
├── cli.py             # argparse, URL/ID resolution, top-level loop
├── core.py            # init_client, tagging, file naming, download orchestration
├── mime_utils.py      # MIME sniffing for cover images (JPEG / PNG magic bytes)
└── text_utils.py      # truncate filename parts to satisfy 255-byte limit
```

Source links:

- [`ymd/__main__.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/__main__.py)
- [`ymd/api.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/api.py)
- [`ymd/cli.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/cli.py)
- [`ymd/core.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/core.py)
- [`ymd/mime_utils.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/mime_utils.py)
- [`ymd/text_utils.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/text_utils.py)

## 2. Authorization

### How the token is obtained

`ymd` does not handle the OAuth dance itself. The user follows the procedure documented at
<https://ym.marshal.dev/token/#implicit-oauth>:

1. Open `https://oauth.yandex.ru/authorize?response_type=token&client_id=23cabbbdc6cd418abb4b39c32c41195d`
2. Log in to a Yandex account and approve the app.
3. The browser is redirected to `https://music.yandex.ru/#access_token=AQAAAA...&token_type=bearer&expires_in=31535645`.
4. The fragment `access_token` is the bearer token to feed to `ymd --token <Токен>`.

The `client_id` `23cabbbdc6cd418abb4b39c32c41195d` is the well-known public client ID used by the
community for Yandex.Music API access. The token is a long-lived (~365 days) OAuth bearer token.

Previous versions (v2) supported browser-cookie based authentication; this was removed in v3 — see
[`MIGRATION.md`](https://github.com/llistochek/yandex-music-downloader/blob/main/MIGRATION.md):

> Для авторизации используйте аргумент `--token`. Аргументы `--browser`, `--user-agent`, `--cookies-path`
> больше не являются валидными, авторизация через cookies невозможна.

### Where the token is stored

**Nowhere.** The token is passed only as a CLI flag (`--token <Токен>`) and lives in process memory
([`ymd/cli.py` L196-L202](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/cli.py#L196-L202)).
A Lidarr plugin will need its own persistent secret store (Lidarr settings DB / connection config).

### HTTP headers used for API requests

These are set inside the upstream `yandex_music` library, not in `ymd`. From
[`MarshalX/yandex-music-api: yandex_music/utils/request_base.py`](https://github.com/MarshalX/yandex-music-api/blob/main/yandex_music/utils/request_base.py):

```python
USER_AGENT = 'Yandex-Music-API'
HEADERS = {
    'X-Yandex-Music-Client': 'YandexMusicAndroid/24023621',
}
```

Authorization header is set in `RequestBase.set_authorization`:

```python
def set_authorization(self, token: str) -> None:
    self.headers.update({'Authorization': f'OAuth {token}'})
```

So every request to `api.music.yandex.net` carries:

```http
Authorization: OAuth AQAAAA...
X-Yandex-Music-Client: YandexMusicAndroid/24023621
User-Agent: Yandex-Music-API
Accept-Language: ru        # if set_language was called; default not set
```

The `X-Yandex-Music-Client` header is what unlocks access to lossless quality (the server treats us as
the Android app build `24023621`). **For a Lidarr plugin this header must be replicated verbatim** —
without it the `get-file-info` endpoint will not return FLAC URLs.

## 3. Yandex Music REST API

### Base URL

```text
https://api.music.yandex.net
```

Default `base_url` of `Client` in `MarshalX/yandex-music-api`. The web frontend
(`music.yandex.ru`) is **only** used to parse user-supplied URLs in `ymd/cli.py`, never as an API target.

### URL/ID parsing (`ymd/cli.py`)

```python
TRACK_RE    = re.compile(r"track/(\d+)")
ALBUM_RE    = re.compile(r"album/(\d+)$")
ARTIST_RE   = re.compile(r"artist/(\d+)$")
PLAYLIST_RE = re.compile(r"playlists/(.+)$")
```

These extract numeric IDs from URLs of the form:

| URL                                                      | Resolved ID                            |
| -------------------------------------------------------- | -------------------------------------- |
| `https://music.yandex.ru/artist/208167`                  | `artist_id = 208167`                   |
| `https://music.yandex.ru/album/294912`                   | `album_id = 294912`                    |
| `https://music.yandex.ru/album/11644078/track/6705392`   | `track_id = 6705392`                   |
| `https://music.yandex.ru/users/<owner>/playlists/<kind>` | `playlist_id = "<owner>/<kind>"`       |

### Search

Search uses `GET /search` with these params (from
[`yandex_music/_client/search.py`](https://github.com/MarshalX/yandex-music-api/blob/main/yandex_music/_client/search.py)):

```python
url = f'{self.base_url}/search'
params = {
    'text': text,
    'nocorrect': str(nocorrect),         # "False" by default
    'type':  type_,                      # 'all' | 'artist' | 'album' | 'track' | 'playlist' | 'user'
                                         # | 'podcast' | 'podcast_episode'
    'page': page,                        # 0-indexed
    'playlist-in-best': str(playlist_in_best),
}
```

Example concrete request:

```http
GET /search?text=arctic+monkeys&nocorrect=False&type=artist&page=0&playlist-in-best=True HTTP/1.1
Host: api.music.yandex.net
Authorization: OAuth AQAAAA...
X-Yandex-Music-Client: YandexMusicAndroid/24023621
```

`ymd` itself does **not** use search at all — it always starts from a known ID. For Lidarr integration
search is the most important endpoint and is fully usable via the same `Client.search` wrapper.

There is also `GET /search/suggest?part=<query>` for incremental suggestions.

### Albums

```python
GET /albums                              # batch: ids as form/query (used by Client.albums)
GET /albums/{album_id}/with-tracks       # full album incl. volumes -> tracks
GET /albums/{album_id}/similar-entities
GET /albums/{album_id}/trailer
```

`ymd` uses `albums_with_tracks` to enumerate every track in an album (`ymd/cli.py` L241-L245):

```python
def album_tracks_gen(album_ids):
    for album_id in album_ids:
        if full_album := client.albums_with_tracks(album_id):
            if volumes := full_album.volumes:
                yield from itertools.chain.from_iterable(volumes)
```

`Album.volumes` is `list[list[Track]]` — outer list is disc/volume index (1, 2, 3, …), inner list is the
tracks of that disc. Multi-disc albums are flattened with `itertools.chain.from_iterable`.

### Artists

From [`yandex_music/_client/artists.py`](https://github.com/MarshalX/yandex-music-api/blob/main/yandex_music/_client/artists.py):

```python
GET /artists                             # batch
GET /artists/{artist_id}/brief-info
GET /artists/{artist_id}/tracks          # params: page, page-size (default 20)
GET /artists/{artist_id}/direct-albums   # params: sort-by=year|rating, page, page-size
GET /artists/{artist_id}/similar
GET /artists/{artist_id}/artist-links
GET /artists/{artist_id}/also-albums
```

`ymd` paginates the artist's own albums via `artists_direct_albums(artist_id, page)`
([`ymd/cli.py` L262-L283](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/cli.py#L262-L283)):

```python
def albums_id_gen() -> Generator[int]:
    has_next = True
    page = 0
    while has_next:
        if albums_info := client.artists_direct_albums(args.artist_id, page):
            for album in albums_info.albums:
                if filter_album(album):
                    yield album.id
                ...
        else:
            break
        if pager := albums_info.pager:
            page = pager.page + 1
            has_next = pager.per_page * page < pager.total
        else:
            break
```

`albums_info.pager` returns `{page, per_page, total}` — standard pagination contract.

### Tracks

```python
GET /tracks                              # batch
GET /tracks/{track_id}/download-info     # LEGACY - returns mp3 only, no key
GET /tracks/{track_id}/supplement
GET /tracks/{track_id}/lyrics            # params: format=TEXT|LRC, timeStamp, sign  (HMAC-signed)
GET /tracks/{track_id}/similar
GET /tracks/{track_id}/full-info
GET /tracks/{track_id}/trailer
```

**Note on `/download-info`:** this is the legacy endpoint MarshalX wraps as
`Client.tracks_download_info(...)`. It returns a list of `DownloadInfo` items with quality variants
(`lq`, `hq`) but **does not include lossless/FLAC and does not include the AES key** for current
encrypted streams. `ymd` **does not use this endpoint** — see the next section.

### Lyrics signing

Lyrics use the same HMAC-SHA256 + Base64 signing scheme as `get-file-info`. The key is the same
`DEFAULT_SIGN_KEY = 'p93jhgh689SBReK6ghtw62'` extracted from the Android app
([`yandex_music/utils/sign_request.py`](https://github.com/MarshalX/yandex-music-api/blob/main/yandex_music/utils/sign_request.py)):

```python
DEFAULT_SIGN_KEY = 'p93jhgh689SBReK6ghtw62'   # Android app sign key

def get_sign_request(track_id, key=DEFAULT_SIGN_KEY) -> Sign:
    timestamp = int(datetime.datetime.now().timestamp())
    message = f'{track_id}{timestamp}'
    hmac_sign = hmac.new(key.encode(), message.encode(), hashlib.sha256).digest()
    return Sign(timestamp=timestamp, value=base64.b64encode(hmac_sign).decode())
```

## 4. Download Info, Track Encryption, Decryption

This is the **most interesting** piece of `ymd` and the only HTTP logic the project owns. It lives
entirely in [`ymd/api.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/api.py).

### The `get-file-info` endpoint

```python
GET https://api.music.yandex.net/get-file-info
    ?ts=<unix_ts>
    &trackId=<track_id>
    &quality=lq|nq|lossless
    &codecs=flac,flac-mp4,mp3,aac,he-aac,aac-mp4,he-aac-mp4
    &transports=encraw
    &sign=<HMAC_SHA256 base64, last char stripped>
```

Source ([`ymd/api.py` L60-L94](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/api.py#L60-L94)):

```python
def get_download_info(track: Track, quality: ApiTrackQuality) -> CustomDownloadInfo:
    client = track.client
    timestamp = int(time.time())
    params = {
        "ts": timestamp,
        "trackId": track.id,
        "quality": quality,                       # "lq" | "nq" | "lossless"
        "codecs": ",".join(FILE_FORMAT_MAPPING.keys()),
        "transports": "encraw",
    }
    hmac_sign = hmac.new(
        DEFAULT_SIGN_KEY.encode(),
        "".join(str(e) for e in params.values()).replace(",", "").encode(),
        hashlib.sha256,
    )
    sign = base64.b64encode(hmac_sign.digest()).decode()[:-1]  # NB: last char dropped
    params["sign"] = sign

    resp = client.request.get("https://api.music.yandex.net/get-file-info", params=params)
    e = resp["downloadInfo"]
    raw_codec = e["codec"]
    file_format = FILE_FORMAT_MAPPING.get(raw_codec)
    return CustomDownloadInfo(
        quality=e["quality"],
        file_format=file_format,
        urls=e["urls"],            # list of CDN URLs to encrypted bytes
        bitrate=e["bitrate"],
        decryption_key=e.get("key"),
    )
```

**Important details for a C# port:**

1. **Signature input** is built from the *values* of `params` joined with empty string, **then commas
   are removed**. With the order `ts, trackId, quality, codecs, transports` (Python 3.7+ preserves
   `dict` insertion order), the message becomes:

   ```text
   1700000000123456lossless"flacflac-mp4mp3aache-aacaac-mp4he-aac-mp4""encraw"
   ```

   …i.e. `f"{ts}{trackId}{quality}{codecs_no_commas}{transports}"`.

2. **Sign key:** the same `p93jhgh689SBReK6ghtw62` as the lyrics endpoint.

3. **The base64 signature has its last character stripped** (`[:-1]`). This drops the `=` padding. A
   C# port must do `Convert.ToBase64String(hmac).TrimEnd('=')[..^1]` or equivalent — match the Python
   slicing exactly.

4. **`transports=encraw`** is what tells the server "give me encrypted-raw transport, I have a
   decryptor". Without it you'd get a different (legacy) response shape.

5. **Response shape (observed):**

   ```json
   {
     "downloadInfo": {
       "trackId": "6705392",
       "quality": "lossless",
       "codec": "flac",
       "urls": [
         "https://s50vla.storage.yandex.net/get-mp3/.../encraw/..."
       ],
       "urlsV2": [...],
       "bitrate": 1411,
       "transport": "encraw",
       "size": 41234567,
       "key": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
     }
   }
   ```

   - `urls` is a list of CDN endpoints; `ymd` picks one at random
     (`random.choice(download_info.urls)`).
   - `key` is a **64-character hex string = 256-bit AES key**, present whenever `transport=encraw`.
   - `codec` values map to containers via `FILE_FORMAT_MAPPING`
     ([`ymd/api.py` L34-L42](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/api.py#L34-L42)):

     ```python
     FILE_FORMAT_MAPPING = {
         "flac":        FileFormat(Container.FLAC, Codec.FLAC),
         "flac-mp4":    FileFormat(Container.MP4,  Codec.FLAC),
         "mp3":         FileFormat(Container.MP3,  Codec.MP3),
         "aac":         FileFormat(Container.MP4,  Codec.AAC),
         "he-aac":      FileFormat(Container.MP4,  Codec.AAC),
         "aac-mp4":     FileFormat(Container.MP4,  Codec.AAC),
         "he-aac-mp4":  FileFormat(Container.MP4,  Codec.AAC),
     }
     ```

### Quality levels

`ymd` exposes three CLI levels mapped to API `quality` strings
([`ymd/api.py` L45-L48](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/api.py#L45-L48),
[`ymd/core.py` L386-L396](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/core.py#L386-L396)):

| CLI `--quality` | `CoreTrackQuality` | API `ApiTrackQuality` | Typical result        |
| --------------- | ------------------ | --------------------- | --------------------- |
| `0`             | `LOW`              | `"lq"`                | AAC ~64 kbps (.m4a)   |
| `1`             | `NORMAL`           | `"nq"`                | AAC ~192 kbps (.m4a)  |
| `2`             | `LOSSLESS`         | `"lossless"`          | FLAC (.flac)          |

Lossless requires a paying Yandex Plus subscription on the account, otherwise the server falls back to
a lower codec/bitrate.

### Decryption (AES-256-CTR with zero nonce)

This is the breakthrough from
[issue #112 comment by @keltecc](https://github.com/llistochek/yandex-music-downloader/issues/112#issuecomment-2812535100).
The encrypted-raw transport ships ciphertext that must be decrypted in-process
([`ymd/api.py` L97-L111](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/api.py#L97-L111)):

```python
def download_track(client: Client, download_info: CustomDownloadInfo) -> bytes:
    data = client.request.retrieve(random.choice(download_info.urls))
    if decryption_key := download_info.decryption_key:
        data = decrypt_data(data, decryption_key)
    return data


def decrypt_data(data: bytes, key: str) -> bytes:
    aes = AES.new(
        key=bytes.fromhex(key),     # 32 raw bytes = AES-256
        nonce=bytes(12),            # 12 zero bytes
        mode=AES.MODE_CTR,          # CTR mode
    )
    return aes.decrypt(data)
```

Summary of the crypto:

- **Algorithm:** AES-256 in CTR mode.
- **Key:** 32 bytes, parsed from the hex string in `downloadInfo.key`.
- **Nonce:** 12 bytes of zeros.
- **Counter:** the remaining 4 bytes (PyCryptodome's default `initial_value=0` and big-endian counter).
- **Mode of operation:** plain CTR — there is no MAC/IV, no per-block tag. Decryption is a single
  pass over the entire stream and emits the original container bytes (FLAC, MP3 or MP4 frames). The
  output is the **complete audio file** ready to be written to disk; no further demuxing is needed.

**C# equivalent** (using `System.Security.Cryptography` is awkward because .NET doesn't ship AES-CTR
directly; use BouncyCastle's `SicBlockCipher` or roll AES-ECB on a counter):

```csharp
// pseudo: BouncyCastle approach
var engine = new AesEngine();
var ctr = new SicBlockCipher(engine);          // SIC == CTR
var iv = new byte[16];                         // 12 zero bytes + 4 zero counter bytes
ctr.Init(forEncryption: false, new ParametersWithIV(new KeyParameter(keyBytes), iv));
// then process the whole stream block-by-block
```

There is **no XOR-with-salt** scheme like older `yandex-music-download` projects assumed. The
`get-file-info?transports=encraw` flow is the current (2024+) DRM and is reportedly stable.

## 5. Download Pipeline (`ymd/core.py`)

End-to-end flow inside `download_track`
([`ymd/core.py` L313-L383](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/core.py#L313-L383)):

1. **Fetch lyrics** (optional) — `track.get_lyrics(format_='LRC')` writes a `*.lrc` side-car;
   `format_='TEXT'` returns plain text to be embedded in the audio tag.
2. **Fetch cover art** — `track.cover_uri` is a template like
   `avatars.yandex.net/get-music-content/12345/abc/%%`; the upstream library calls
   `track.download_cover_bytes(size=<NxN>)`, substituting `%%` with `400x400` (default) or `orig` for
   the highest-resolution variant. MIME (`image/jpeg` or `image/png`) is detected from magic bytes
   ([`ymd/mime_utils.py`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/mime_utils.py)).
3. **Get download info & encrypted bytes** — `api.get_download_info` then `api.download_track`.
4. **Atomic write**: bytes are written to `.yandex-music-downloader.<sha256>.tmp` in the target
   directory, tags are applied to the temp file, and finally `temporary_file.rename(target_path)`
   ([`write_via_temporary_file`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/core.py#L417-L434)).

### Tagging (`set_tags`, `ymd/core.py` L182-L310)

Per container:

- **MP3** — ID3v2.4 frames via `mutagen.id3`: `TIT2` (title), `TALB` (album), `TPE1` (artists),
  `TPE2` (album artists), `TDRC` (release date), `TRCK`, `TPOS`, `TCON` (genre), `USLT` (lyrics),
  `APIC` (cover, type 3 = front cover), `WOAF` (track web URL on music.yandex.ru).
- **MP4 / M4A** — atoms `\xa9nam`, `\xa9alb`, `\xa9ART`, `aART`, `\xa9day`, `trkn`, `disk`,
  `\xa9gen`, `\xa9lyr`, `covr`, `\xa9cmt`. At compatibility level 1 (default), multi-value tags
  (artists, album artists) are joined with `"; "` instead of using mutagen's array form, which most
  players handle better.
- **FLAC** — Vorbis comments: `title`, `album`, `artist`, `albumartist`, `date`, `tracknumber`,
  `discnumber`, `genre`, `lyrics`, `comment`. Cover is added as a `Picture` block with
  `type=PictureType.COVER_FRONT`.

All formats embed a `track_url = "https://music.yandex.ru/album/{album.id}/track/{track.id}"` for
traceability.

### File naming

Default pattern: `#album-artist/#album/#number - #title.<ext>`
([`ymd/core.py` L52](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/core.py#L52)).

Placeholders ([`prepare_base_path`](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/core.py#L134-L179)):

| Placeholder       | Source                                            |
| ----------------- | ------------------------------------------------- |
| `#album-artist`   | `track.albums[0].artists[0].name`                 |
| `#track-artist`   | `track.artists[0].name`                           |
| `#artist-id`      | `track.artists[0].id`                             |
| `#album`          | `track.albums[0].title (+ version)`               |
| `#album-id`       | `track.albums[0].id`                              |
| `#title`          | `track.title (+ version)`                         |
| `#track-id`       | `track.id`                                        |
| `#number`         | `track_position.index`                            |
| `#number-padded`  | `zfill(len(str(album.track_count)))`              |
| `#year`           | `track.albums[0].year`                            |

Sanitization regexes:

```python
SAFE_PATH_CLEAR_RE   = re.compile(r"([^\w\-\'() ]|^\s+|\s+$)")   # default - aggressive
UNSAFE_PATH_CLEAR_RE = re.compile(r"[/\\]+")                     # --unsafe-path
```

Path parts are also truncated by `text_utils.remove_characters_to_satisfy_byte_length` to fit each
filesystem component into 255 bytes minus the longest audio suffix (`.flac` = 5 → 250 effective bytes).

### Networking robustness

`init_client` wraps `client.request._request_wrapper` with a retry loop catching `NetworkError`
([`ymd/core.py` L97-L122](https://github.com/llistochek/yandex-music-downloader/blob/main/ymd/core.py#L97-L122)).
Defaults: `--timeout 20`, `--tries 20`, `--retry-delay 5`.

## 6. CLI Surface

Single command, mutually exclusive ID selectors, plus token. Reference output:

```text
yandex-music-downloader \
    --token "<oauth_token>" \
    --quality 2 \
    --url "https://music.yandex.ru/artist/208167" \
    --dir /music \
    --path-pattern "#album-artist/#album/#number-padded - #title" \
    --embed-cover \
    --cover-resolution original \
    --lyrics-format lrc \
    --skip-existing \
    --only-music \
    --stick-to-artist
```

Mutually exclusive selectors:

```text
--artist-id   <id>
--album-id    <id>
--track-id    <id>
--playlist-id <owner>/<kind>
--url / -u    <any music.yandex.ru URL>
```

Common flags worth mirroring in a Lidarr config:

| Flag                       | Purpose                                                                                |
| -------------------------- | -------------------------------------------------------------------------------------- |
| `--quality 0\|1\|2`        | AAC 64 / AAC 192 / FLAC                                                                |
| `--lyrics-format`          | `none` (default) / `text` (USLT) / `lrc` (side-car .lrc with timestamps)               |
| `--embed-cover`            | Embed cover in audio file rather than writing `cover.jpg` next to it                   |
| `--cover-resolution`       | Integer px (default 400) or literal `original`                                         |
| `--skip-existing`          | Don't re-download if file with any of `{.mp3,.flac,.m4a}` exists at the target path    |
| `--only-music`             | When walking an artist, skip non-music releases (`album.meta_type != "music"`)         |
| `--stick-to-artist`        | When walking an artist, skip albums whose primary artist differs                       |
| `--compatibility-level`    | 0 = strict mutagen tags, 1 = joined-string multi-value tags for MP4 (default 1)        |
| `--delay <sec>`            | Sleep between requests (default 0; the mobile token is not aggressively rate-limited)  |
| `--timeout` / `--tries` / `--retry-delay` | Network resilience                                                      |

## 7. Notes for Lidarr Integration

Lidarr plugins typically split responsibilities into:

- **Indexer** — searches releases, returns torrent/usenet-like result objects (artist/album/year,
  size, codec, source, magnet URI or direct URL).
- **Download client** — accepts the chosen result, downloads files into a configured directory,
  reports progress/completion back.

### Metadata model

The Yandex.Music JSON models map cleanly onto Lidarr's expectations:

```text
Search.artists.results[] -> Artist  { id, name, various, composer, cover.uri,
                                      counts: { tracks, direct_albums, also_albums, ... },
                                      genres: [...] }

Search.albums.results[] -> Album   { id, title, year, release_date (ISO),
                                     track_count, genre, meta_type,
                                     artists: [Artist...],
                                     cover_uri (template with %%),
                                     volumes: [[Track...], [Track...]] }  // when .with-tracks

Search.tracks.results[] -> Track   { id, title, version, duration_ms,
                                     albums: [Album...], artists: [Artist...],
                                     cover_uri, available, lyrics_info: {has_available_*} }
```

Useful fields per concept:

| Lidarr concept       | Yandex.Music field(s)                                              |
| -------------------- | ------------------------------------------------------------------ |
| Artist primary key   | `Artist.id` (integer)                                              |
| Artist name          | `Artist.name`                                                      |
| Album primary key    | `Album.id` (integer)                                               |
| Album title          | `Album.title` (+ `Album.version`)                                  |
| Album year           | `Album.year`, `Album.release_date` (ISO 8601 with TZ)              |
| Album track count    | `Album.track_count`                                                |
| Track primary key    | `Track.id`                                                         |
| Track number / disc  | `Track.albums[0].track_position.{index, volume}`                   |
| Track duration       | `Track.duration_ms`                                                |
| Cover URL            | `track.cover_uri` / `album.cover_uri`, replace `%%` with `400x400` or `orig` |
| Bitrate              | `downloadInfo.bitrate` (e.g. `192`, `320`, `1411`)                 |
| Codec / Container    | `downloadInfo.codec` -> see `FILE_FORMAT_MAPPING`                  |
| File size hint       | `downloadInfo.size` (bytes, observed in encraw response)           |

### MusicBrainz mapping — **not provided by the API**

There is no MusicBrainz ID anywhere in the Yandex.Music payload. The plugin has two practical options:

1. **Match externally**: when Lidarr searches for an MBID-keyed album, the plugin must do a fuzzy
   search on `artist + album + year` against `/search?type=album` and pick the best hit. Stripping
   `Album.version` (often something like "Deluxe Edition") and normalizing punctuation will be
   necessary.
2. **Use Yandex IDs as opaque release IDs**: expose Lidarr release entries with a stable URI scheme
   like `yandexmusic://album/{album_id}` and let the indexer surface them; the download client then
   parses the URI back into the album/track IDs.

### Integration touch points

1. **Auth UI**: A "Get token" wizard that opens the OAuth URL and asks the user to paste the
   `access_token` fragment back. Save under the indexer / download client settings; pass as the
   `Authorization: OAuth <token>` header.

2. **Required headers** for every call to `api.music.yandex.net`:
   ```http
   Authorization: OAuth <token>
   X-Yandex-Music-Client: YandexMusicAndroid/24023621
   User-Agent: Yandex-Music-API
   ```
   Skipping `X-Yandex-Music-Client` will degrade `get-file-info` results.

3. **Indexer search** (`/search?type=album` / `?type=artist`):
   - Map result fields above into Lidarr's `ReleaseInfo` / `AlbumInfo`.
   - Quality / size: do a HEAD-like probe of `get-file-info` if the user has set a preferred
     quality, **or** advertise generic sizes and resolve the actual codec later (the encrypted
     transport returns precise `size` and `bitrate`).

4. **Download client**:
   - Resolve the album via `albums_with_tracks`.
   - For each available track (`track.available == true`):
     - Compute `get-file-info` with desired quality.
     - GET one of `urls` (random, then retry the next one on failure).
     - Decrypt AES-256-CTR with zero nonce as described.
     - Write to the temp file, embed tags via TagLib# (the .NET analogue of mutagen) — Lidarr
       already depends on TagLib# for its own metadata writing, so reuse it.
     - Rename atomically; emit a "track complete" event with file path.
   - Aggregate per-track results into one "release complete" notification to Lidarr.

5. **Throttling**: the README explicitly says mobile-token requests are not rate-limited aggressively.
   Still, a configurable inter-request delay (defaulting to 0–250 ms) is sensible to avoid burning
   the token if Yandex tightens limits.

6. **Lossless availability**: requires Yandex Plus on the linked account. Plugin should detect a
   downgraded `quality` in the response and surface a warning to the user (Lidarr can choose to
   reject the release if it requested FLAC but got AAC).

7. **What `ymd` does NOT do that Lidarr will care about:**
   - No release-level dedup (Lidarr handles that).
   - No incremental "what's new" feed — to implement an RSS-like indexer, the plugin will need to
     poll `artists_direct_albums` for tracked artists and compare against the last seen album IDs
     (Yandex.Music has no native "since" filter).
   - No torrent/magnet workflow — everything is direct HTTPS, so the plugin should masquerade as a
     "usenet"-style direct-download client in Lidarr's vocabulary.

### Reference parameter table for `get-file-info`

For copy-paste convenience, the exact request the C# port has to emit:

```text
URL:    https://api.music.yandex.net/get-file-info
Method: GET
Query:
  ts          = <unix epoch seconds>
  trackId     = <numeric track id>
  quality     = "lq" | "nq" | "lossless"
  codecs      = "flac,flac-mp4,mp3,aac,he-aac,aac-mp4,he-aac-mp4"
  transports  = "encraw"
  sign        = base64(HMAC_SHA256("p93jhgh689SBReK6ghtw62",
                       ts + trackId + quality + codecs.Replace(",", "") + "encraw"))
                .TrimEnd('=')[..^1]    # drop one trailing char (matches Python `[:-1]`)
Headers:
  Authorization:          OAuth <token>
  X-Yandex-Music-Client:  YandexMusicAndroid/24023621
  User-Agent:             Yandex-Music-API
```

Response (interesting fields only):

```json
{
  "downloadInfo": {
    "codec": "flac",
    "quality": "lossless",
    "bitrate": 1411,
    "size": 41234567,
    "transport": "encraw",
    "urls": ["https://...storage.yandex.net/get-mp3/.../encraw/..."],
    "key": "<64 hex chars = AES-256 key>"
  }
}
```

### Acknowledged third-party work (per the `ymd` README)

- [MarshalX/yandex-music-api](https://github.com/MarshalX/yandex-music-api) — the actual REST client.
- [@ArtemBay's lossless script](https://github.com/MarshalX/yandex-music-api/issues/656#issuecomment-2306542725)
  — original recipe for hitting `get-file-info` with `quality=lossless`.
- [@keltecc's decryption note](https://github.com/llistochek/yandex-music-downloader/issues/112#issuecomment-2812535100)
  — the AES-CTR / zero-nonce decryption procedure adopted in `ymd/api.py`.
- [@leowerd's artist-name fix](https://github.com/llistochek/yandex-music-downloader/issues/93#issuecomment-2960210879)
  — correct multi-artist handling on compilations.
