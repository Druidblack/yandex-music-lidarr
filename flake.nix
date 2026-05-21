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

        dotnet = pkgs.dotnetCorePackages.sdk_8_0;
      in
      {
        devShells.default = pkgs.mkShell {
          name = "yandex-music-lidarr";

          packages = with pkgs; [
            dotnet
            omnisharp-roslyn
            csharpier

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
            export DOTNET_ROOT="${dotnet}"
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
            export PATH="$HOME/.dotnet/tools:$PATH"

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
