.PHONY: help build run release test clean
-include secrets.mk

help:
	$(info ${HELP_MESSAGE})
	@exit 0
	
build:
	dotnet build MiHomeLib -c Debug

run:
	dotnet run --project MiHomeConsole

test:
	dotnet test MiHomeUnitTests

clean:
	dotnet clean

release:
	dotnet build MiHomeLib -c Release

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
	build		Build app
	release		Build release
	test		Run unit tests
	run		Run console application
	clean		Clean
	publish-nuget	Upload nuget package to nuget.org
	publish-github	Upload nuget package to github packages repo	
endef
