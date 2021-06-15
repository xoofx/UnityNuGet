> The NuGet package needs to respect a few constraints in order to be listed in the curated list:
> - [ ] Add a link to the NuGet package: https://www.nuget.org/packages/XXX
> - [ ] It must have non-preview versions (e.g 1.0.0 but not 1.0.0-preview.1)
> - [ ] It must provide .NETStandard2.0 assemblies as part of its package
> - [ ] The lowest version added must be the lowest .NETStandard2.0 version available
> - [ ] The package has been tested with the Unity editor 
> - [ ] The package has been tested with a Unity standalone player
>   - if the package is not compatible with standalone player, please add a comment to a Known issues section to the top level readme.md
> - [ ] All package dependencies with .NETStandard 2.0 target must be added to the PR (respecting the same rules above)
>   - Note that if a future version of the package adds a new dependency, this dependency will have to be added manually as well
