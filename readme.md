# UnityNuGet [![Build Status](https://github.com/xoofx/UnityNuGet/workflows/ci/badge.svg?branch=master)](https://github.com/xoofx/UnityNuGet/actions)

<img align="right" width="160px" height="160px" src="img/unitynuget.png">

This project provides a seamlessly integration of a [curated list](registry.json) of NuGet packages within the Unity Package Manager.

> DISCLAIMER: This is not an official service provided by Unity Technologies Inc.

In order to use this service you simply need to edit the `Packages/manifest.json` in your project and add the following scoped registry:

```json
{
  "scopedRegistries": [
    {
      "name": "Unity NuGet",
      "url": "https://unitynuget-registry.azurewebsites.net",
      "scopes": [
        "org.nuget"
      ]
    }
  ],
  "dependencies": {
     "org.nuget.scriban":  "2.1.0"
  }
}
```

When opening the Package Manager Window, you should see a few packages coming from NuGet (with the postfix text ` (NuGet)`)

![UnityEditorWithNuGet](img/unity_editor_with_nuget.jpg)

## Adding a package to the registry

This service provides only a [curated list](registry.json) of NuGet packages

Your NuGet package needs to respect a few constraints in order to be listed in the curated list:

- It must have non-preview versions (e.g `1.0.0` but not `1.0.0-preview.1`)
- It must provide `.NETStandard2.0` assemblies as part of its package

You can send a PR to this repository to modify the [registry.json](registry.json) file (don't forget to maintain the alphabetical order)

I recommend also to **specify the lowest version of your package that has support for `.NETStandard2.0`** upward so that other packages depending on your package have a chance to work with.

Beware that **all transitive dependencies of the package** must be **explicitly listed** in the registry as well.

> NOTE: We reserve the right to decline a package to be available through this service

## Compatibility

Only compatible with **`Unity 2019.1`** and potentially with newer version.

> NOTE: This service is currently only tested with **`Unity 2019.1`**
>
> It may not work with a more recent version of Unity

## Docker

Example of a basic docker-compose.yml file:

```yaml
services:
  unitynuget:
    build: .
    ports:
      - 5000:80
    volumes:
      - ./unity_packages:/app/unity_packages
```

There is a complete example in [examples/docker](examples/docker).

## FAQ

### Where is hosted this service?

On Azure through my own Azure credits coming from my MVP subscription, enjoy!

### Why can't you add all NuGet packages?

The reason is that many NuGet packages are not compatible with Unity, or do not provide .NETStandard2.0 assemblies or are not relevant for being used within Unity.

Also currently the Package Manager doesn't provide a way to filter easily packages, so the UI is currently not adequate to list lots of packages.

### Why does it require .NETStandard2.0?

Since 2019.1, Unity is now compatible with `.NETStandard2.0` and it is the .NET profile that is preferred to be used

Having a `.NETStandard2.0` for NuGet packages for Unity can ensure that the experience to add a package to your project is consistent and well supported.

### How this service is working?

This project implements a simplified compatible NPM server in C# using ASP.NET Core and converts NuGet packages to Unity packages before serving them. 

Every 10min, packages are updated from NuGet so that if a new version is published, from the curated list of NuGet packages, it will be available through this service.

Once converted, these packages are cached on the disk on the server.

## License

This software is released under the [BSD-Clause 2 license](https://opensource.org/licenses/BSD-2-Clause). 

## Author

Alexandre Mutel aka [xoofx](http://xoofx.com)
