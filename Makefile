.PHONY: help build build-gw-setup gw run release test clean
-include secrets.mk

help:
	$(info ${HELP_MESSAGE})
	@exit 0
	
build:
	dotnet build MiHomeLib -c Debug

build-gw-setup:
	dotnet build MiHomeSetup -c Debug

run:
	dotnet run --project MiHomeConsole

test:
	dotnet test MiHomeUnitTests

clean:
	dotnet clean

release:
	dotnet pack MiHomeLib -c Release -o MiHomeLib/bin/Release

publish-nuget: check-version check-secrets release
	dotnet nuget push MiHomeLib/bin/Release/MiHomeLib.${v}.nupkg --api-key $(NUGET_SECRET) --source https://api.nuget.org/v3/index.json

publish-github: check-version check-secrets release
	dotnet nuget push MiHomeLib/bin/Release/MiHomeLib.${v}.nupkg --api-key $(GITHUB_SECRET) --source https://nuget.pkg.github.com/sergey-brutsky/index.json

check-version:
ifndef v
	$(error Parameter 'v' is undefined. Must be valid nuget package version)
endif

check-secrets:
ifeq (,$(wildcard secrets.mk))
	$(error secrets.mk doesnt exist, you need to download it first !)
endif

define HELP_MESSAGE

Usage: $ make [TARGETS]

TARGETS
	build		Build library
	build-gw-setup	Build gateway setup application
	gw check IP [model=MODEL] [token=TOKEN | token_file=PATH] [key=KEY | key_file=PATH]
			Detect model and check gateway readiness
	gw setup IP [model=MODEL] [token=TOKEN | token_file=PATH] [key=KEY | key_file=PATH]
			Enable Gateway 2 LAN access or prepare MQTT gateway access
	release		Create release version of nuget package
	test			Run unit tests
	run			Run console application
	clean		Clean build artifacts
	publish-nuget	Upload nuget package to nuget.org
	publish-github	Upload nuget package to github nuget repo	
endef

# GNU make treats command-line words as targets. Consume only the gateway
# command and positional host; pass option values through the environment.
ifeq ($(firstword $(MAKECMDGOALS)),gw)
export GW_COMMAND := $(word 2,$(MAKECMDGOALS))
export GW_HOST := $(if $(host),$(host),$(word 3,$(MAKECMDGOALS)))
# Treat a supplied key as literal data, including any dollar signs.
override key := $(value key)
export model token token_file key key_file

ifeq ($(filter check setup disable-lan,$(GW_COMMAND)),)
$(error Usage: make gw check|setup|disable-lan IP [model=MODEL] [token=TOKEN | token_file=PATH] [key=KEY | key_file=PATH])
endif
ifneq ($(word 4,$(MAKECMDGOALS)),)
$(error Extra gateway arguments must use make variables such as model=MODEL and token_file=PATH)
endif
ifneq ($(host),)
ifneq ($(word 3,$(MAKECMDGOALS)),)
$(error Specify the host either positionally or with host=IP)
endif
endif
ifeq ($(strip $(GW_HOST)),)
$(error Gateway IP is required, either positionally or with host=IP)
endif
gw:
	@set -eu; \
	set -- "$$GW_COMMAND" --host "$$GW_HOST"; \
	if [ -n "$${model:-}" ]; then set -- "$$@" --model "$$model"; fi; \
	if [ -n "$${token:-}" ]; then set -- "$$@" --token "$$token"; fi; \
	if [ -n "$${token_file:-}" ]; then set -- "$$@" --token-file "$$token_file"; fi; \
	if [ -n "$${key:-}" ]; then set -- "$$@" --key "$$key"; fi; \
	if [ -n "$${key_file:-}" ]; then set -- "$$@" --key-file "$$key_file"; fi; \
	dotnet run --project MiHomeSetup -- "$$@"

.PHONY: $(wordlist 2,3,$(MAKECMDGOALS))
$(wordlist 2,3,$(MAKECMDGOALS)):
	@:
endif
