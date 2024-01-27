---
layout: page
title: "Installation"
permalink: /Documentation/Installation.html
---

You've got several choices for how to install VidCoder. Files for any version or flavor can be found on the [GitHub Releases page](https://github.com/RandomEngy/VidCoder/releases).

## Installer (Recommended)

VidCoder uses [Velopack](https://github.com/velopack/velopack), which will automatically install VidCoder to `%localappdata%` and run it. It will automatically download and install the required .NET runtime as part of setup.

Updates are done seamlessly. By default it never bothers you about updates: if there's a new version it downloads it in the background and applies it on the next launch. In Global options you can change this to prompt to apply updates right away. If you choose to apply the update it will relaunch immediately with the new version, no UAC prompts or installer wizards.

You can change the install location by running the installer .exe with `--installto {dir}`. This does not yet support protected directories like `Program Files`. It is only available for versions >= 10.1.

## Portable

This is a single .exe file that runs VidCoder without the need for installation. It's compiled in standalone mode so it doesn't need to have the .NET runtime installed. By default it keeps all of the local app files beside the executable, though it will use VidCoder.sqlite if it finds one in `%appdata%`.

This will be a little slower to launch than the other flavors as it first needs to extract all the binaries to a temporary directory before running them.

## Zip

For when you want exact control. Just unzip and put it wherever you want. It doesn't do auto-updates.