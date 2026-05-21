# Lidarr Plugin Development Guide

A practical reference for building Lidarr plugins, focused on the use-case of
integrating a streaming service (e.g. Yandex.Music) as an indexer plus download
client. All information below was gathered from the live Lidarr `plugins`
branch, the Servarr Wiki, and the two reference plugins maintained by `TrevTV`
(Tidal, Deezer) along with the `RicherTunes/Lidarr.Plugin.Common` helper
library.

---

## 1. State of the Plugin System

### 1.1 Branches

Lidarr's plugin subsystem lives on a dedicated branch in the upstream repo:

- `plugins` — the long-lived branch that hosts plugin support code:
  <https://github.com/Lidarr/Lidarr/tree/plugins>
- `plugins-dev` — an in-flight development branch for plugin work.
- `master` / `develop` — stable channels that **do not** ship plugin support.

The Servarr Wiki is explicit: *"Plug-in capabilities are now part of the main
branch of Lidarr, but only in the nightly branch."* The user-facing channel
name that maps to the plugin codebase is `nightly` for native installs and the
`pr-plugins` / `nightly` tags for the LinuxServer.io / Hotio Docker images.
There is no stable release with plugins yet, and downgrading after enabling
plugins corrupts the database because new protocol identifiers cannot be
parsed by the older binary.

References:

- <https://wiki.servarr.com/en/lidarr/plugins>
- <https://github.com/Servarr/Wiki/blob/master/lidarr/plugins.md>

### 1.2 Runtime and Language

Plugins are first-class .NET assemblies loaded by the Lidarr host. The toolchain
is fixed by the upstream `Directory.Build.props` and `global.json`:

- **Language:** C# (Lidarr is a Sonarr fork, both C#).
- **SDK pin:** `.NET SDK 8.0.404` with `rollForward: latestMinor`
  (see `src/global.json` of `Lidarr.Plugin.Tidal`).
- **Target framework:** `net8.0` (Lidarr ships `net6.0` and `net8.0` plugin
  outputs; the asset filter in `PluginService.cs` selects `netX.0.zip`
  matching the host runtime).
- **Build system:** MSBuild via `dotnet build`; multi-project solutions
  are normal (`.sln` plus one or more `.csproj`).

### 1.3 No Official Documentation

There is no official Lidarr plugin SDK, no NuGet package, and no published
plugin manifest format. The community workflow is to clone the upstream
`Lidarr` source as a git submodule and build against `Lidarr.Core.csproj` /
`Lidarr.Common.csproj` project references directly. The Servarr Wiki explicitly
states: *"both plugins and documentation are community-driven, with no official
recommendations existing at this time, and each developer supports their own
plugin."*

---

## 2. Plugin Types

Lidarr extension points usable from a plugin (visible in
`src/NzbDrone.Core/` on the `plugins` branch):

| Type | Base class / interface | What it does |
| --- | --- | --- |
| Indexer | `HttpIndexerBase<TSettings>` (or `IndexerBase<TSettings>` / `IIndexer`) | Discovers releases (artists/albums) and returns `ReleaseInfo` objects. |
| Download Client | `DownloadClientBase<TSettings>` (`IDownloadClient`) | Fetches the actual audio files for a `RemoteAlbum`. |
| Download Protocol | `IDownloadProtocol` | Marker type binding indexer and download client together (alongside `UsenetDownloadProtocol` / `TorrentDownloadProtocol`). |
| Import List | `ImportListBase<TSettings>` (`IImportList`) | Adds artists/albums to monitor from external sources. |
| Notification | `NotificationBase<TSettings>` (`INotification`) | Reacts to library events. |
| Metadata Provider | `MetadataBase<TSettings>` (`IMetadata`) | Writes sidecar files. |
| Provider settings | `IProviderConfig` / `IIndexerSettings` | Strongly typed config surface rendered as a form in the UI. |

For a streaming service like Yandex.Music the canonical combination is
**Indexer + Download Client + Custom Download Protocol** — exactly what the
Tidal and Deezer reference plugins implement.

---

## 3. Reference Plugins

Reading these repositories end-to-end is the fastest way to learn the system:

- **Tidal** — <https://github.com/TrevTV/Lidarr.Plugin.Tidal>
- **Deezer** — <https://github.com/TrevTV/Lidarr.Plugin.Deezer>
- **Qobuz** — referenced in the wiki as part of the TrevTV suite
- **Tubifarry (YouTube Music + Slskd)** — <https://github.com/TypNull/Tubifarry>
- **Lidarr.Plugin.Common (shared helpers)** —
  <https://github.com/RicherTunes/Lidarr.Plugin.Common>

The Tidal and Deezer plugins share the exact same skeleton; the only deltas are
the upstream API client (`TidalSharp` vs `DeezNET`) and the encryption pieces
needed for Deezer. Yandex.Music will follow the same pattern with a custom
`YandexMusicSharp` or equivalent client.

---

## 4. Repository Layout

The directory layout used by both reference plugins (taken from
`Lidarr.Plugin.Tidal`):

```text
.
├── .github/workflows/        # CI: builds, packages, publishes GitHub releases
├── ext/
│   └── Lidarr/               # git submodule pointing at Lidarr@plugins
├── src/
│   ├── Directory.Build.props # MSBuild conventions shared across projects
│   ├── Directory.Build.targets
│   ├── NuGet.config          # Lidarr-specific feeds (Taglib, SQLite, etc.)
│   ├── global.json           # .NET SDK pin
│   ├── stylecop.json
│   ├── Lidarr.Plugin.Tidal.sln
│   ├── Lidarr.Plugin.Tidal/
│   │   ├── Lidarr.Plugin.Tidal.csproj
│   │   ├── Plugin.cs                          # entry point
│   │   ├── TidalAPI.cs                        # singleton wrapper around upstream API client
│   │   ├── ILRepack.targets                   # merges helper libs into one DLL
│   │   ├── Indexers/Tidal/
│   │   │   ├── Tidal.cs                       # HttpIndexerBase<...>
│   │   │   ├── TidalSettings.cs               # IIndexerSettings
│   │   │   ├── TidalRequestGenerator.cs       # IIndexerRequestGenerator
│   │   │   ├── TidalParser.cs                 # IParseIndexerResponse
│   │   │   └── TidalSearchResponse.cs         # DTOs
│   │   ├── Download/
│   │   │   ├── TidalDownloadProtocol.cs       # IDownloadProtocol marker
│   │   │   └── Clients/Tidal/
│   │   │       ├── Tidal.cs                   # DownloadClientBase<...>
│   │   │       ├── TidalSettings.cs           # IProviderConfig
│   │   │       ├── TidalProxy.cs              # bridge to background queue
│   │   │       ├── MetadataUtilities.cs       # FLAC/MP3 tagging
│   │   │       └── Queue/
│   │   │           ├── DownloadItem.cs        # one album = one work item
│   │   │           └── DownloadTaskQueue.cs   # background processor
│   │   └── Properties/
│   └── TidalSharp/                            # upstream Tidal API client (sibling project)
```

Key points:

- **`ext/Lidarr` as a submodule.** Project references reach into
  `..\\..\\ext\\Lidarr\\src\\NzbDrone.Core\\Lidarr.Core.csproj`. There is no
  NuGet package for Lidarr — you must source-reference its `Core` assembly.
- **Plugin classes live under `NzbDrone.*` namespaces.** Even though the
  product is called Lidarr, all framework types use the original Sonarr-era
  `NzbDrone.Core.*` namespaces (the upstream `Directory.Build.props` still
  rewrites `Lidarr` → `NzbDrone` at the namespace level).
- **One single output assembly is required.** Lidarr loads plugins with a
  private `AssemblyLoadContext` but the discovery code expects every plugin
  type to live inside one DLL. Both Tidal and Deezer use `ILRepack` to merge
  their dependencies — this is documented as a workaround for *"a bug in
  Lidarr's plugin system"* in the Tidal README.

---

## 5. The Plugin Entry Point

Lidarr discovers plugins by scanning loaded assemblies for any subclass of
`NzbDrone.Core.Plugins.Plugin`. The base class is tiny — see
[`src/NzbDrone.Core/Plugins/Plugin.cs`](https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Plugins/Plugin.cs):

```csharp
namespace NzbDrone.Core.Plugins
{
    public interface IPlugin
    {
        string Name { get; }
        string Owner { get; }
        string GithubUrl { get; }
        PluginVersion InstalledVersion { get; }
        PluginVersion AvailableVersion { get; set; }
    }

    public abstract class Plugin : IPlugin
    {
        public virtual string Name { get; }
        public virtual string Owner { get; }
        public virtual string GithubUrl { get; }
        public PluginVersion InstalledVersion { get; /* parsed from assembly attrs */ }
        public PluginVersion AvailableVersion { get; set; }
    }
}
```

The Tidal plugin defines exactly one such class:

```csharp
// src/Lidarr.Plugin.Tidal/Plugin.cs
namespace NzbDrone.Core.Plugins
{
    public class TidalPlugin : Plugin
    {
        public override string Name => "Tidal";
        public override string Owner => "TrevTV";
        public override string GithubUrl => "https://github.com/TrevTV/Lidarr.Plugin.Tidal";
    }
}
```

There is **no `plugin.json` manifest**. Metadata that the host needs at install
time (compatible framework, minimum Lidarr version) comes from:

- The packaged asset name on the GitHub release (must contain `net8.0.zip`).
- The `<MinimumLidarrVersion>` MSBuild property in the `.csproj` (used by
  the build, not by the host directly — `PluginService` reads release notes
  to find a `MinimumLidarrVersion: x.y.z.w` marker).

The version reported in the UI comes from
`AssemblyInformationalVersionAttribute`, which the build pipelines stamp
with the GitHub release tag.

---

## 6. Project File Conventions

### 6.1 The plugin `.csproj`

Pulled from `Lidarr.Plugin.Tidal/Lidarr.Plugin.Tidal.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0</TargetFrameworks>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ILRepack.Lib.MSBuild.Task" Version="2.0.34.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="TagLibSharp" Version="2.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\ext\Lidarr\src\NzbDrone.Core\Lidarr.Core.csproj" />
    <ProjectReference Include="..\TidalSharp\TidalSharp.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <MinimumLidarrVersion>2.2.4.4129</MinimumLidarrVersion>
  </PropertyGroup>

  <!-- During local debug, copy the merged DLL next to a real Lidarr install -->
  <Target Condition="'$(Configuration)' == 'Debug'" Name="PostBuild" AfterTargets="ILRepacker">
    <Exec ContinueOnError="true"
          Command="COPY &quot;$(TargetPath)&quot; &quot;C:\ProgramData\Lidarr\plugins\TrevTV\Lidarr.Plugin.Tidal&quot;" />
  </Target>
</Project>
```

Notes:

- `CopyLocalLockFileAssemblies=true` lets `ILRepack` see every dependency in
  the output folder before merging.
- `ProjectReference` pulls Lidarr.Core directly. This means the build
  effectively compiles the relevant slice of Lidarr too. Some repos pin the
  submodule to a known-good commit.
- The `PostBuild` target is a developer-loop trick — Debug builds get dropped
  into the local Lidarr plugin directory automatically.

### 6.2 The shared `Directory.Build.props`

`src/Directory.Build.props` is copied verbatim from upstream Lidarr. Important
flags used by plugin projects:

```xml
<PluginProject>false</PluginProject>
<PluginProject Condition="$(MSBuildProjectName.StartsWith('Lidarr.Plugin'))">true</PluginProject>
...
<OutputPath Condition="'$(PluginProject)'=='true'">
  $(LidarrRootDir)_plugins\$(TargetFramework)\$(MSBuildProjectName)
</OutputPath>
...
<AppendTargetFrameworkToOutputPath Condition="'$(PluginProject)'=='true'">false</AppendTargetFrameworkToOutputPath>
```

Anything whose project name starts with `Lidarr.Plugin` is treated as a plugin
project and emitted to `_plugins/<framework>/<name>/`. This convention is how
the CI scripts know which directory to zip up for releases.

### 6.3 `NuGet.config` and SDK pin

Both reference plugins ship the upstream feeds because Lidarr uses
non-public TagLib/SQLite forks:

```xml
<!-- src/NuGet.config -->
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="Taglib" value="https://pkgs.dev.azure.com/Lidarr/Lidarr/_packaging/Taglib/nuget/v3/index.json" />
    <add key="SQLite" value="https://pkgs.dev.azure.com/Servarr/Servarr/_packaging/SQLite/nuget/v3/index.json" />
    <add key="FluentMigrator" value="https://pkgs.dev.azure.com/Servarr/Servarr/_packaging/FluentMigrator/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

```jsonc
// src/global.json
{
  "sdk": {
    "version": "8.0.404",
    "rollForward": "latestMinor"
  }
}
```

---

## 7. Implementing an Indexer

For a streaming-service indexer the natural base class is
`HttpIndexerBase<TSettings>`. The interface chain is:

```text
IIndexer  ->  IndexerBase<TSettings>  ->  HttpIndexerBase<TSettings>
```

Both `RSS` (RSS = "recent releases" cron) and `Search` (artist/album lookups
triggered by Lidarr's search engine) flow through the same chain.

### 7.1 The indexer class

```csharp
// src/Lidarr.Plugin.Tidal/Indexers/Tidal/Tidal.cs
public class Tidal : HttpIndexerBase<TidalIndexerSettings>
{
    public override string Name => "Tidal";
    public override string Protocol => nameof(TidalDownloadProtocol);
    public override bool SupportsRss => false;       // streaming services rarely have a useful RSS feed
    public override bool SupportsSearch => true;
    public override int PageSize => 100;
    public override TimeSpan RateLimit => TimeSpan.FromSeconds(2);

    private readonly ITidalProxy _tidalProxy;

    public Tidal(ITidalProxy tidalProxy,
                 IHttpClient httpClient,
                 IIndexerStatusService indexerStatusService,
                 IConfigService configService,
                 IParsingService parsingService,
                 Logger logger)
        : base(httpClient, indexerStatusService, configService, parsingService, logger)
    {
        _tidalProxy = tidalProxy;
    }

    public override IIndexerRequestGenerator GetRequestGenerator() => new TidalRequestGenerator
    {
        Settings = Settings,
        Logger = _logger
    };

    public override IParseIndexerResponse GetParser() => new TidalParser { Settings = Settings };
}
```

Things to copy for a Yandex.Music indexer:

- `Protocol` returns the **name** of your `IDownloadProtocol` type. Lidarr
  matches indexer releases to download clients via this string, so the
  indexer and the download client must share the same value.
- Constructor parameters are resolved by Lidarr's DI container. You can ask
  for your own `IYandexProxy` and it will be wired up automatically as long
  as the type is discoverable in the same assembly.

### 7.2 Settings class

Settings drive the auto-generated form in the Lidarr UI. Each field uses
`[FieldDefinition]` from `NzbDrone.Core.Annotations`:

```csharp
// src/Lidarr.Plugin.Tidal/Indexers/Tidal/TidalSettings.cs
public class TidalIndexerSettings : IIndexerSettings
{
    private static readonly TidalIndexerSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Tidal URL", HelpText = "Use this to sign into Tidal.")]
    public string TidalUrl { get => TidalAPI.Instance?.Client?.GetPkceLoginUrl() ?? ""; set { } }

    [FieldDefinition(0, Label = "Redirect Url", Type = FieldType.Textbox)]
    public string RedirectUrl { get; set; } = "";

    [FieldDefinition(1, Label = "Config Path", Type = FieldType.Textbox,
                     HelpText = "Directory where account information is stored so it can be reloaded later.")]
    public string ConfigPath { get; set; } = "";

    [FieldDefinition(2, Type = FieldType.Number, Label = "Early Download Limit",
                     Unit = "days", Advanced = true,
                     HelpText = "Time before release date Lidarr will download from this indexer, empty is no limit")]
    public int? EarlyReleaseLimit { get; set; }

    // Required by IIndexerSettings even when unused
    public string BaseUrl { get; set; } = "";

    public NzbDroneValidationResult Validate() =>
        new(Validator.Validate(this));
}

public class TidalIndexerSettingsValidator : AbstractValidator<TidalIndexerSettings>
{
    public TidalIndexerSettingsValidator()
    {
        RuleFor(x => x.ConfigPath).IsValidPath();
    }
}
```

Available `FieldType` values include `Textbox`, `Password`, `Checkbox`,
`Number`, `Select`, `Path`, `Tag`. Use `Advanced = true` to hide a field behind
the "Show Advanced Settings" toggle. Field validation uses `FluentValidation`.

### 7.3 Request generator

`IIndexerRequestGenerator` returns batches of HTTP requests grouped into
"tiers". A tier is a fallback list — Lidarr fans out the first tier, only
falling back to the next tier if results are empty.

```csharp
public class TidalRequestGenerator : IIndexerRequestGenerator
{
    private const int PageSize = 100;
    private const int MaxPages = 3;

    public TidalIndexerSettings Settings { get; set; }
    public Logger Logger { get; set; }

    public IndexerPageableRequestChain GetRecentRequests()
    {
        // Streaming services have no real RSS. Return a harmless dummy so that
        // Lidarr's "Test" button on save has something to call.
        var chain = new IndexerPageableRequestChain();
        chain.Add(GetRequests("never gonna give you up"));
        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria c)
    {
        var chain = new IndexerPageableRequestChain();
        chain.AddTier(GetRequests($"{c.ArtistQuery} {c.AlbumQuery}"));
        return chain;
    }

    public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria c)
    {
        var chain = new IndexerPageableRequestChain();
        chain.AddTier(GetRequests(c.ArtistQuery));
        return chain;
    }

    private IEnumerable<IndexerRequest> GetRequests(string query)
    {
        for (var page = 0; page < MaxPages; page++)
        {
            var url = TidalAPI.Instance.GetAPIUrl("search", new Dictionary<string, string>
            {
                ["query"]  = query,
                ["limit"]  = $"{PageSize}",
                ["types"]  = "albums,tracks",
                ["offset"] = $"{page * PageSize}",
            });

            var req = new IndexerRequest(url, HttpAccept.Json);
            req.HttpRequest.Method = HttpMethod.Get;
            req.HttpRequest.Headers.Add("Authorization",
                $"{TidalAPI.Instance.Client.ActiveUser.TokenType} {TidalAPI.Instance.Client.ActiveUser.AccessToken}");
            yield return req;
        }
    }
}
```

Search criteria objects expose:

- `AlbumSearchCriteria` — `ArtistQuery`, `AlbumQuery`, `Artist`, `Album`
  with full Lidarr entity references and MusicBrainz IDs.
- `ArtistSearchCriteria` — `ArtistQuery`, `Artist` plus MBID.

The Yandex.Music backend should expose at least two query types: artist text
search and album text search. If the API supports lookups by ISRC/UPC, prefer
those because MusicBrainz exports both.

### 7.4 Parser → `ReleaseInfo`

This is where the foreign API response is mapped into Lidarr's universal
release shape. The full `ReleaseInfo` contract
([`src/NzbDrone.Core/Parser/Model/ReleaseInfo.cs`](https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Parser/Model/ReleaseInfo.cs)):

```csharp
public class ReleaseInfo
{
    public string Guid { get; set; }                 // Must be globally unique per release
    public string Title { get; set; }                // Human-readable title; parsed for codec/quality cues
    public long Size { get; set; }                   // Bytes — even an estimate is fine
    public string DownloadUrl { get; set; }          // Opaque to Lidarr, passed to your download client
    public string InfoUrl { get; set; }              // Opened from the UI "info" button
    public string Artist { get; set; }
    public string Album { get; set; }
    public string DownloadProtocol { get; set; }     // Must equal nameof(YourDownloadProtocol)
    public DateTime PublishDate { get; set; }
    public string Container { get; set; }            // Free-form quality marker, you choose the vocabulary
    public string Codec { get; set; }
    public List<Language> Languages { get; set; }
    public IndexerFlags IndexerFlags { get; set; }
}
```

Tidal's parser shows the recommended mapping:

```csharp
private static ReleaseInfo ToReleaseInfo(TidalSearchResponse.Album x, AudioQuality bitrate)
{
    var result = new ReleaseInfo
    {
        Guid             = $"Tidal-{x.Id}-{bitrate}",                   // unique per (album, quality)
        Artist           = x.Artists.First().Name,
        Album            = x.Title,
        DownloadUrl      = x.Url,                                       // Tidal URL is parsed by DownloadItem.From
        InfoUrl          = x.Url,
        PublishDate      = ParsePublishDate(x),
        DownloadProtocol = nameof(TidalDownloadProtocol)
    };

    switch (bitrate)
    {
        case AudioQuality.LOW:             result.Codec = "AAC";  result.Container = "96";              break;
        case AudioQuality.HIGH:            result.Codec = "AAC";  result.Container = "320";             break;
        case AudioQuality.LOSSLESS:        result.Codec = "FLAC"; result.Container = "Lossless";        break;
        case AudioQuality.HI_RES_LOSSLESS: result.Codec = "FLAC"; result.Container = "24bit Lossless";  break;
    }

    // Size is estimated from duration × average bitrate, Tidal doesn't expose exact size
    result.Size  = x.Duration * BitsPerSecond(bitrate);
    result.Title = $"{x.Artists.First().Name} - {x.Title} ({year}) [{format}] [WEB]";
    return result;
}
```

Critical mapping rules learned from the reference plugins:

1. **`Guid` must be deterministic and unique per quality.** Emitting one
   `ReleaseInfo` per (album, quality) combination lets Lidarr's quality
   profile pick the best one.
2. **`Title` is parsed by Lidarr's quality detector.** Suffix it with
   `[FLAC]`, `[MP3 320]`, `[24bit Lossless]`, `[WEB]` etc. — the same
   tokens Lidarr already understands from torrent release names.
3. **`DownloadUrl` is opaque** to Lidarr; the download client receives it
   verbatim via `RemoteAlbum.Release.DownloadUrl`. Encode whatever your
   download client needs to identify the album (full URL, custom scheme,
   JSON blob, etc.).
4. **`Container` is the quality marker** picked up by `DownloadItem.From`
   on the client side. Pick a stable vocabulary and use it in both
   indexer and download client.

---

## 8. Implementing a Download Client

### 8.1 Download protocol marker

Just a marker type. Lidarr ships built-in `UsenetDownloadProtocol` and
`TorrentDownloadProtocol` — you add your own:

```csharp
// src/Lidarr.Plugin.Tidal/Download/TidalDownloadProtocol.cs
namespace NzbDrone.Core.Indexers
{
    public class TidalDownloadProtocol : IDownloadProtocol { }
}
```

For Yandex.Music: `YandexMusicDownloadProtocol`. Both the indexer's `Protocol`
property and every `ReleaseInfo.DownloadProtocol` must use this exact name.

### 8.2 The download client class

```csharp
// src/Lidarr.Plugin.Tidal/Download/Clients/Tidal/Tidal.cs
public class Tidal : DownloadClientBase<TidalSettings>
{
    private readonly ITidalProxy _proxy;

    public Tidal(ITidalProxy proxy,
                 IConfigService configService,
                 IDiskProvider diskProvider,
                 IRemotePathMappingService remotePathMappingService,
                 ILocalizationService localizationService,
                 Logger logger)
        : base(configService, diskProvider, remotePathMappingService, localizationService, logger)
    {
        _proxy = proxy;
    }

    public override string Protocol => nameof(TidalDownloadProtocol);
    public override string Name => "Tidal";

    public override IEnumerable<DownloadClientItem> GetItems()
    {
        foreach (var item in _proxy.GetQueue(Settings))
        {
            item.DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false);
            yield return item;
        }
    }

    public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
        => _proxy.Download(remoteAlbum, Settings);

    public override void RemoveItem(DownloadClientItem item, bool deleteData)
    {
        if (deleteData) DeleteItemData(item);
        _proxy.RemoveFromQueue(item.DownloadId, Settings);
    }

    public override DownloadClientInfo GetStatus() => new()
    {
        IsLocalhost       = true,
        OutputRootFolders = new() { new OsPath(Settings.DownloadPath) }
    };

    protected override void Test(List<ValidationFailure> failures) { /* nothing to verify */ }
}
```

What the base class gives you (excerpt from
`src/NzbDrone.Core/Download/DownloadClientBase.cs`):

- `Definition` and `Settings` properties wired by the DI container.
- A `RetryStrategy` resilience pipeline (Polly) ready for HTTP calls.
- `DeleteItemData(item)` for safely removing completed downloads from disk.
- The abstract surface to implement:
  - `Download(RemoteAlbum, IIndexer)` — start the download, return a stable
    `DownloadId`.
  - `GetItems()` — current queue snapshot.
  - `RemoveItem(item, deleteData)` — cancel or purge.
  - `GetStatus()` — surface in the UI: is this localhost, what root folder
    will downloads land in.
  - `Test(failures)` — settings sanity check.

### 8.3 Background queue pattern

Lidarr expects `Download(...)` to return quickly. Both reference plugins use
the same proxy pattern:

```csharp
public interface ITidalProxy
{
    List<DownloadClientItem> GetQueue(TidalSettings settings);
    Task<string> Download(RemoteAlbum remoteAlbum, TidalSettings settings);
    void RemoveFromQueue(string downloadId, TidalSettings settings);
}

public class TidalProxy : ITidalProxy
{
    private readonly DownloadTaskQueue _taskQueue;

    public TidalProxy(ICacheManager cacheManager, Logger logger)
    {
        _taskQueue = new DownloadTaskQueue(500, null, logger);
        _taskQueue.StartQueueHandler();
    }

    public async Task<string> Download(RemoteAlbum remoteAlbum, TidalSettings settings)
    {
        _taskQueue.SetSettings(settings);
        var item = await DownloadItem.From(remoteAlbum);
        await _taskQueue.QueueBackgroundWorkItemAsync(item);
        return item.ID;
    }
    /* ...GetQueue, RemoveFromQueue, ToDownloadClientItem... */
}
```

`DownloadTaskQueue` is a hand-rolled background processor:

- A capped `Channel<T>` plus a `Task.Run` loop consuming `DownloadItem`s.
- Each `DownloadItem` pulls track IDs from the album URL, downloads them
  with a `SemaphoreSlim(3)` (configurable parallelism), writes tags via
  TagLibSharp, and updates `Progress`/`Status`.

The same approach should work for Yandex.Music. Key design points:

1. **Return a stable `DownloadId`** from `Download(...)` — `Guid.NewGuid()`
   is fine, but it must survive Lidarr polling cycles.
2. **`GetItems()` returns the whole queue** (queued, downloading, completed).
   Lidarr keeps polling until items disappear after import.
3. **Write tracks into per-album folders** under `Settings.DownloadPath`.
   Lidarr's import system scans this folder, recognises the audio files,
   matches them to the queued album via `DownloadId`, and moves them into
   the library.

### 8.4 Download settings

```csharp
public class TidalSettings : IProviderConfig
{
    private static readonly TidalSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Download Path", Type = FieldType.Textbox)]
    public string DownloadPath { get; set; } = "";

    [FieldDefinition(1, Label = "Extract FLAC From M4A",
        Type = FieldType.Checkbox,
        HelpText = "Extracts FLAC data from Tidal-provided M4A files.",
        HelpTextWarning = "Requires FFMPEG and FFProbe available to Lidarr.")]
    public bool ExtractFlac { get; set; }

    [FieldDefinition(5, Label = "Download Delay",
        Type = FieldType.Checkbox,
        HelpText = "Adds a delay between track downloads to dodge rate-limiting.")]
    public bool DownloadDelay { get; set; }

    public NzbDroneValidationResult Validate() => new(Validator.Validate(this));
}
```

`IProviderConfig` is intentionally minimal compared to `IIndexerSettings` —
you only need the `Validate()` method.

---

## 9. MusicBrainz Considerations

Lidarr is rigidly tied to MusicBrainz as its metadata source. Every artist
and album in the library has an MBID; the import pipeline matches downloaded
files to album records by matching tags (artist, album, track count) to
metadata pulled from MusicBrainz.

Implications for a Yandex.Music plugin:

1. **No MBID for the imported album = no library import.** Files that can't
   be matched to a known artist+album sit in the queue indefinitely. The
   user must either add the artist with MusicBrainz first and *then* let
   Lidarr search, or accept that imports will fail.

2. **The cleanest workflow is the standard one:**
   - User adds an artist in Lidarr — Lidarr resolves the MBID via search.
   - User wants an album — Lidarr fires `AlbumSearchCriteria` to all indexers
     (including yours).
   - Your indexer searches Yandex.Music by artist name + album title, returns
     `ReleaseInfo` records.
   - Lidarr picks the best release per quality profile and hands the
     `RemoteAlbum` (which already has the MBID-backed `Album` / `Artist`
     attached) to your download client.
   - Your client downloads the tracks, tags them with the MusicBrainz IDs
     it received via `remoteAlbum.Album`, writes them under `Settings.DownloadPath`.
   - Lidarr's import scanner picks them up and moves them.

3. **Tag the files with MBIDs.** `RemoteAlbum.Album.ForeignAlbumId` is the
   MBID Lidarr expects on the imported files. Use TagLibSharp to write
   `MUSICBRAINZ_ALBUMID`, `MUSICBRAINZ_ARTISTID`, `MUSICBRAINZ_TRACKID`,
   `MUSICBRAINZ_RELEASETRACKID` tags into FLAC/MP3 files. The Tidal plugin
   does this in `MetadataUtilities.cs`.

4. **Yandex album IDs ≠ MBIDs.** Cache the mapping between Yandex album IDs
   and MBIDs locally if you want better hit rates on `RSS`/recent releases;
   external lookups by ISRC are also possible but require a MusicBrainz
   round-trip.

5. **Albums that exist on Yandex but not on MusicBrainz** are effectively
   un-importable — Lidarr won't track them as library entries. This is a
   fundamental Lidarr limitation, not a plugin one.

---

## 10. Build, Packaging, Release

### 10.1 Local development loop

```bash
# clone with submodule
git clone --recurse-submodules https://github.com/<owner>/Lidarr.Plugin.YandexMusic
cd Lidarr.Plugin.YandexMusic

# build
dotnet build src/Lidarr.Plugin.YandexMusic.sln \
       --configuration Debug

# output lands here:
#   _plugins/net8.0/Lidarr.Plugin.YandexMusic/Lidarr.Plugin.YandexMusic.dll
```

The `Directory.Build.props` redirects plugin output to
`_plugins/<framework>/<name>/`. For Debug, the `PostBuild` target copies the
DLL into your local Lidarr plugin folder.

### 10.2 Install location

Lidarr expects plugins under the application data directory:

| Platform | Path |
| --- | --- |
| Linux Docker | `/config/plugins/<Owner>/<PluginName>/` |
| Linux native | `~/.config/Lidarr/plugins/<Owner>/<PluginName>/` |
| Windows | `C:\ProgramData\Lidarr\plugins\<Owner>\<PluginName>\` |
| macOS | `~/.config/Lidarr/plugins/<Owner>/<PluginName>/` |

Each plugin folder contains the merged DLL (plus any non-ILRepacked
dependencies). `InstallPluginService` (Lidarr/plugins branch) writes there
when the user installs via the UI.

### 10.3 Installing via the UI

From the Servarr Wiki and the Tidal README:

1. Switch Lidarr to the `nightly` / `pr-plugins` branch.
2. Open `Settings → General`, enable Advanced, set Update Branch to
   `nightly`, save. Native installs auto-update; Docker users repoint
   their image tag (`ghcr.io/hotio/lidarr:pr-plugins`,
   `lscr.io/linuxserver/lidarr:nightly`).
3. Navigate to `System → Plugins`.
4. Paste the GitHub repository URL (`https://github.com/<owner>/<repo>`)
   into the "GitHub URL" field and click Install. Branch/tree pinning is
   supported with `#branch` syntax (parsed by
   `PluginService.ParseRepositoryInput`).
5. Restart Lidarr. The plugin's indexer/download client now appears in
   the "Other" section of the Indexers and Download Clients pages.

`PluginService.GetRemotePlugin` queries `https://api.github.com/repos/<owner>/<repo>/releases`,
filters releases whose assets contain `net8.0.zip` and whose body satisfies
the embedded `MinimumLidarrVersion:` marker. The newest matching release is
downloaded, extracted to the plugin folder, and the assemblies are loaded on
next restart.

### 10.4 CI release packaging

The reference plugins use GitHub Actions to:

1. Restore submodules (`Lidarr` at the right commit).
2. Run `dotnet build --configuration Release` against the `net8.0`
   target.
3. Zip the contents of `_plugins/net8.0/Lidarr.Plugin.X/` into
   `Lidarr.Plugin.X.net8.0.zip`.
4. Use `gh release create` to publish the zip with a body that contains
   `MinimumLidarrVersion: x.y.z.w` so the Lidarr installer can filter
   compatibility.

You can copy `.github/workflows/release.yml` from `Lidarr.Plugin.Tidal`
verbatim and only adjust project paths and `MinimumLidarrVersion`.

### 10.5 ILRepack for one-DLL plugins

Both reference plugins use `ILRepack.Lib.MSBuild.Task` plus a custom
`ILRepack.targets` file that merges helper libraries into the final assembly.
The Tidal README documents the reason:

> All of these libraries have been merged into the final plugin assembly
> due to (what I believe is) a bug in Lidarr's plugin system.

In practice, anything that isn't already part of Lidarr.Core (Newtonsoft.Json,
TagLibSharp, your own API client) must be merged or loaded reflectively.
ILRepack remains the path of least resistance.

---

## 11. Shared Helper Library

`RicherTunes/Lidarr.Plugin.Common`
(<https://github.com/RicherTunes/Lidarr.Plugin.Common>) is a community library
that bundles shared utilities used by the Qobuz/Tidal-derived plugin family:

- Resilience policies (Polly pipelines tuned for streaming APIs).
- Token management and refresh patterns.
- ILRepack packaging helpers.
- Shared cache abstractions.

It targets the same `net8.0` runtime and is meant to be vendored into the
plugin's own assembly (`AssemblyLoadContext` per plugin — see Servarr Wiki —
means you cannot share a DLL between two plugins at runtime). Treat it as a
starting set of utilities, not a hard dependency.

---

## 12. Suggested Architecture for the Yandex.Music Plugin

Putting it all together — recommended layout for the Yandex.Music plugin:

```text
Lidarr.Plugin.YandexMusic/
├── ext/Lidarr/                                 # submodule -> Lidarr@plugins
├── src/
│   ├── Lidarr.Plugin.YandexMusic.sln
│   ├── Directory.Build.props (copied from upstream)
│   ├── Directory.Build.targets
│   ├── NuGet.config (Lidarr feeds + nuget.org)
│   ├── global.json (.NET 8 SDK pin)
│   ├── YandexMusicSharp/                       # YANDEX API client (separate project)
│   │   ├── YandexMusicSharp.csproj
│   │   ├── Auth/...                            # OAuth / token / X-Yandex-Music-Client-* headers
│   │   ├── Api/AlbumsClient.cs
│   │   ├── Api/SearchClient.cs
│   │   ├── Api/TracksClient.cs
│   │   ├── Codec/Decryptor.cs                  # Yandex serves encrypted MP3/FLAC streams
│   │   └── Models/...
│   └── Lidarr.Plugin.YandexMusic/
│       ├── Lidarr.Plugin.YandexMusic.csproj
│       ├── Plugin.cs                           # YandexMusicPlugin : Plugin
│       ├── YandexMusicAPI.cs                   # singleton wrapper
│       ├── ILRepack.targets
│       ├── Indexers/YandexMusic/
│       │   ├── YandexMusic.cs                  # HttpIndexerBase<YandexMusicIndexerSettings>
│       │   ├── YandexMusicSettings.cs
│       │   ├── YandexMusicRequestGenerator.cs
│       │   ├── YandexMusicParser.cs
│       │   └── YandexMusicSearchResponse.cs
│       ├── Download/
│       │   ├── YandexMusicDownloadProtocol.cs
│       │   └── Clients/YandexMusic/
│       │       ├── YandexMusic.cs              # DownloadClientBase<YandexMusicSettings>
│       │       ├── YandexMusicSettings.cs
│       │       ├── YandexMusicProxy.cs
│       │       ├── MetadataUtilities.cs        # MBID tagging via TagLibSharp
│       │       └── Queue/
│       │           ├── DownloadItem.cs
│       │           └── DownloadTaskQueue.cs
│       └── Properties/
└── .github/workflows/release.yml
```

Key design decisions to lock in early:

- **Container vocabulary.** Pick now: `"MP3 192"`, `"MP3 320"`, `"FLAC"`,
  `"FLAC 24bit"`. Use the same strings on both sides.
- **Identifier in `DownloadUrl`.** Embedding the Yandex album ID (e.g.
  `yandex-music://album/12345/quality/flac`) is cleaner than smuggling
  a real Yandex URL and parsing it.
- **Auth model.** Yandex.Music uses an OAuth token. Persist it under
  `Settings.ConfigPath` (configurable directory), exactly the way Tidal
  persists its PKCE state. The `[FieldDefinition]` form should not store
  the raw token — store the path to the token directory.
- **Rate limiting.** Yandex aggressively throttles unfamiliar clients;
  re-use the Tidal `DownloadDelay` knob and serialise track downloads with
  `SemaphoreSlim(2)` initially.
- **Stream decryption.** Yandex.Music serves AES-encrypted streams (the
  "secret signature" handshake). The encryption code belongs in
  `YandexMusicSharp`, behind a `Decryptor` API; the Lidarr plugin should
  never touch raw cryptography.

---

## 13. Useful References

Lidarr / Servarr:

- Plugin branch source: <https://github.com/Lidarr/Lidarr/tree/plugins>
- Plugin base class: <https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Plugins/Plugin.cs>
- Plugin service (resolver / installer):
  <https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Plugins/PluginService.cs>,
  <https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Plugins/InstallPluginService.cs>
- `HttpIndexerBase`: <https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Indexers/HttpIndexerBase.cs>
- `DownloadClientBase`: <https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Download/DownloadClientBase.cs>
- `ReleaseInfo`: <https://github.com/Lidarr/Lidarr/blob/plugins/src/NzbDrone.Core/Parser/Model/ReleaseInfo.cs>
- Servarr Wiki — plugin install / branch guide:
  <https://wiki.servarr.com/en/lidarr/plugins>
- Servarr Wiki source: <https://github.com/Servarr/Wiki/blob/master/lidarr/plugins.md>

Reference plugins:

- Tidal: <https://github.com/TrevTV/Lidarr.Plugin.Tidal>
- Deezer: <https://github.com/TrevTV/Lidarr.Plugin.Deezer>
- Tubifarry (YouTube Music + Slskd): <https://github.com/TypNull/Tubifarry>
- Shared utilities: <https://github.com/RicherTunes/Lidarr.Plugin.Common>

Community channels (mentioned in the wiki and reference READMEs):

- Lidarr Discord — `#plugins` channel is where TrevTV and other plugin
  authors discuss compatibility issues.
- r/Lidarr — episodic threads on plugin install, especially around branch
  switching pitfalls.
- Servarr Wiki Issues — the canonical place to file documentation gaps:
  <https://github.com/Servarr/Wiki/issues>
