{
  description = "Lidarr plugin for Yandex.Music — development environment";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs =
    { self
    , nixpkgs
    , flake-utils
    ,
    }:
    flake-utils.lib.eachDefaultSystem (
      system:
      let
        pkgs = import nixpkgs { inherit system; };

        # .NET 8 SDK is provided by Homebrew (formula `dotnet@8`, keg-only) rather
        # than nixpkgs to avoid lengthy from-source builds on darwin.  The shell
        # hook below wires its keg path into PATH / DOTNET_ROOT.
        brewDotnetRoot = "/opt/homebrew/opt/dotnet@8/libexec";
      in
      {
        devShells.default = pkgs.mkShell {
          name = "yandex-music-lidarr";

          packages = with pkgs; [
            git
            gh

            jq
            zip
            unzip
            curl
            tree

            markdownlint-cli
            editorconfig-checker

            nixpkgs-fmt
          ];

          shellHook = ''
            if [ -d "${brewDotnetRoot}" ]; then
              export DOTNET_ROOT="${brewDotnetRoot}"
              export PATH="${brewDotnetRoot}:$HOME/.dotnet/tools:$PATH"
            else
              echo "warn: ${brewDotnetRoot} not found — install with: brew install dotnet@8"
            fi

            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

            echo ""
            echo "yandex-music-lidarr dev shell"
            echo "  .NET SDK : $(dotnet --version 2>/dev/null || echo 'n/a')"
            echo "  gh       : $(gh --version 2>/dev/null | head --lines=1 || echo 'n/a')"
            echo ""
          '';
        };

        formatter = pkgs.nixpkgs-fmt;
      }
    );
}
