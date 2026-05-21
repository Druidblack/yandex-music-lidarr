# Yandex.Music Lidarr plugin - convenience wrappers around `dotnet`.
#
# Lidarr.Core.csproj (consumed via the ext/Lidarr submodule) inherits its
# own Directory.Build.props which enables TreatWarningsAsErrors and the
# NuGet audit warning NU1902 for its MailKit dependency.  Disabling
# NuGetAudit on the command line keeps that warning out of our build
# without touching the submodule.

SOLUTION    := src/Lidarr.Plugin.YandexMusic.sln
DOTNET      := dotnet
CONFIG      ?= Release
BUILD_FLAGS := --configuration $(CONFIG) --property:NuGetAudit=false

COMPOSE     ?= docker compose

.PHONY: help restore build rebuild test clean format lint lidarr-up lidarr-down lidarr-restart lidarr-logs lidarr-reset lidarr-shell

help:
	@echo "Targets:"
	@echo "  restore          restore NuGet packages"
	@echo "  build            build solution (CONFIG=Release|Debug, default Release)"
	@echo "  rebuild          clean + build"
	@echo "  test             run tests"
	@echo "  clean            remove build output and intermediate dirs"
	@echo "  format           apply csharpier formatting"
	@echo "  lint             check formatting without modifying files"
	@echo "  lidarr-up        start the local Lidarr nightly container"
	@echo "  lidarr-down      stop the local Lidarr container"
	@echo "  lidarr-restart   restart Lidarr (pick up a freshly built plugin)"
	@echo "  lidarr-logs      tail Lidarr logs"
	@echo "  lidarr-shell     open a shell inside the Lidarr container"
	@echo "  lidarr-reset     stop Lidarr and wipe its volumes (DATA LOSS)"

restore:
	$(DOTNET) restore $(SOLUTION) --property:NuGetAudit=false

build:
	$(DOTNET) build $(SOLUTION) $(BUILD_FLAGS)

rebuild: clean build

test:
	$(DOTNET) test $(SOLUTION) $(BUILD_FLAGS) --no-build

clean:
	rm -rf _plugins
	rm -rf src/*/bin src/*/obj
	rm -rf ext/Lidarr/_output ext/Lidarr/_temp

format:
	$(DOTNET) tool run csharpier format src

lint:
	$(DOTNET) tool run csharpier check src

lidarr-up: build
	$(COMPOSE) up --detach
	@echo ""
	@echo "Lidarr starting up at http://localhost:$${LIDARR_PORT:-18686}"
	@echo "First boot takes ~30s; check progress with: make lidarr-logs"

lidarr-down:
	$(COMPOSE) down

lidarr-restart: build
	$(COMPOSE) restart lidarr

lidarr-logs:
	$(COMPOSE) logs --follow --tail 100 lidarr

lidarr-shell:
	$(COMPOSE) exec lidarr /bin/bash

lidarr-reset:
	$(COMPOSE) down --volumes
