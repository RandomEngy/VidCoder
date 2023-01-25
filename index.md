---
layout: home
title: Home
version: 219
---

VidCoder is an open-source DVD/Blu-ray ripping and video transcoding application for Windows. It uses [HandBrake](http://handbrake.fr/) as its encoding engine.

{% for release in site.github.releases %}
{% unless release.tag_name == "v6.46" %}
{% unless release.prerelease %}
  {% assign releaseNotes = release.body %}
  {% assign sourceUrl = release.zipball_url %}
  {% assign tag = release.tag_name %}
  {% break %}
{% endunless %}
{% endunless %}
{% endfor %}
{% assign version = tag | remove: "v" %}
{% capture installerUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ tag }}/VidCoder-{{ version }}.exe{% endcapture %}
{% capture portableUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ tag }}/VidCoder-{{ version }}-Portable.exe{% endcapture %}
{% capture notesUrl %}https://github.com/RandomEngy/VidCoder/releases/tag/{{ tag }}{% endcapture %}

{% for release in site.github.releases %}
{% if release.prerelease %}
  {% assign betaReleaseNotes = release.body %}
  {% assign betaSourceUrl = release.zipball_url %}
  {% assign betaTag = release.tag_name %}
  {% break %}
{% endif %}
{% endfor %}
{% assign betaVersion = betaTag | remove: "v" | remove: "-beta" %}
{% capture betaInstallerUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ betaTag }}/VidCoder-{{ betaVersion }}-Beta.exe{% endcapture %}
{% capture betaPortableUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ betaTag }}/VidCoder-{{ betaVersion }}-Beta-Portable.exe{% endcapture %}
{% capture betaNotesUrl %}https://github.com/RandomEngy/VidCoder/releases/tag/{{ betaTag }}{% endcapture %}

<table class="releasesTable">
<tr><td colspan="4">
<h2 class="versionHeader">Latest Stable: {{ version }}</h2>
</td></tr>
<tr>
<td><a class="button" href="{{ installerUrl }}">Download (Installer)</a></td>
<td><a href="{{ portableUrl }}" class="secondaryInstallLink">Portable (.exe)</a></td>
<td><a href="{{ sourceUrl }}" class="secondaryInstallLink">Source (.zip)</a></td>
<td><a href="{{ notesUrl }}" class="secondaryInstallLink">Release Notes</a></td>
</tr>
</table>

<!--<table class="releasesTable">
<tr><td colspan="4">
<h2 class="versionHeader betaVersionHeader">Latest Beta: {{ betaVersion }}</h2>
</td></tr>
<tr>
<td><a href="{{ betaInstallerUrl }}">Download (Installer)</a></td>
<td><a href="{{ betaPortableUrl }}" class="secondaryInstallLink">Portable (.exe)</a></td>
<td><a href="{{ betaSourceUrl }}" class="secondaryInstallLink">Source (.zip)</a></td>
<td><a href="{{ betaNotesUrl }}" class="secondaryInstallLink">Release Notes</a></td>
</tr>
</table>-->

![Main UI screenshot]({{ site.baseurl }}/images/main.png)

<iframe width="814" height="480" src="https://www.youtube.com/embed/5YEZHZghj0k" frameborder="0" allowfullscreen></iframe>

## Feature List

* Multi-threaded
* MP4, MKV containers
* Completely integrated encoding pipeline: everything is in one process and no huge intermediate temporary files
* H.264, H.265, MPEG-4, MPEG-2, VP8, Theora video
* Hardware-accelerated encoding with AMD VCE, Nvidia NVENC and Intel QuickSync
* AAC, MP3, Vorbis, AC3, FLAC audio encoding and AAC/AC3/MP3/DTS/DTS-HD passthrough
* Target bitrate, size or quality for video
* 2-pass encoding
* Decomb, detelecine, deinterlace, rotate, reflect, chroma smooth, colorspace filters
* Powerful batch encoding with simultaneous encodes
* Customizable Pickers to automatically pick audio and subtitle tracks, destination, titles and more
* Instant source previews
* Creates small encoded preview clips
* Pause, resume encoding

![Preview window]({{ site.baseurl }}/images/preview.png)

![Encoding settings]({{ site.baseurl }}/images/encoding_settings.png)

## Prerequisites
You'll need the [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0). If you don't have it, VidCoder will prompt you to download and install it. The Portable version is self-contained and does not require any .NET Runtime to be installed.

You *do not* need to install HandBrake for VidCoder to work.

**Blu-rays and DVDs**
VidCoder can rip Blu-rays and DVDs but does not defeat the encryption found on commercial discs. You've got several options to remove it:
* [AnyDVD HD](https://www.redfox.bz/en/anydvdhd.html) ($122, removes encryption on the fly for Blu-ray and DVD, good support for all discs)
* [Passkey for Blu-ray/DVD](https://www.dvdfab.cn/passkey-for-blu-ray.htm) ($168, removes encryption on the fly for Blu-ray and DVD, good support for all discs)
* [Passkey Lite](https://www.dvdfab.cn/passkey-lite.htm) (Free, removes encryption on the fly for Blu-ray and DVD, does *not* support all discs)
* [MakeMKV](http://www.makemkv.com/) (Free, requires copying to the hard drive before encoding, supports Blu-ray and DVD)
* [libdvdcss](https://github.com/allienx/libdvdcss-dll) (Free, supports DVD only)
* [WinX DVD Ripper](https://www.winxdvd.com/) ($60, supports DVD only, requires copying to the hard drive before encoding)

## Reporting Bugs
If you encounter any bugs with encoding, please see if they happen with the official HandBrake client as well. If the problem is reproducible on HandBrake, submit the issue to them. If it's only VidCoder that has the problem, submit it here.

## Languages
![English]({{ site.baseurl }}/images/flags/english.png){:class="flagImage"} ![Spanish]({{ site.baseurl }}/images/flags/spanish.png){:class="flagImage"} ![German]({{ site.baseurl }}/images/flags/german.png){:class="flagImage"} ![French]({{ site.baseurl }}/images/flags/french.png){:class="flagImage"} ![Italian]({{ site.baseurl }}/images/flags/italian.png){:class="flagImage"} ![Portuguese]({{ site.baseurl }}/images/flags/portuguese.png){:class="flagImage"} ![Brazilian Portuguese]({{ site.baseurl }}/images/flags/portuguese_brazilian.png){:class="flagImage"} ![Dutch]({{ site.baseurl }}/images/flags/dutch.png){:class="flagImage"} ![Greek]({{ site.baseurl }}/images/flags/greek.png){:class="flagImage"} ![Turkish]({{ site.baseurl }}/images/flags/turkish.png){:class="flagImage"} ![Bosnian]({{ site.baseurl }}/images/flags/bosnian.png){:class="flagImage"} ![Korean]({{ site.baseurl }}/images/flags/korean.png){:class="flagImage"} ![Czech]({{ site.baseurl }}/images/flags/czech.png){:class="flagImage"} ![Polish]({{ site.baseurl }}/images/flags/polish.png){:class="flagImage"} ![Indonesian]({{ site.baseurl }}/images/flags/indonesian.png){:class="flagImage"} ![Russian]({{ site.baseurl }}/images/flags/russian.png){:class="flagImage"} ![Chinese Simplified]({{ site.baseurl }}/images/flags/chinese_simplified.png){:class="flagImage"} ![Chinese Traditional]({{ site.baseurl }}/images/flags/chinese_traditional.png){:class="flagImage"} ![Japanese]({{ site.baseurl }}/images/flags/japanese.png){:class="flagImage"} ![Hungarian]({{ site.baseurl }}/images/flags/hungarian.png){:class="flagImage"} ![Georgian]({{ site.baseurl }}/images/flags/georgian.png){:class="flagImage"} ![Basque]({{ site.baseurl }}/images/flags/basque.png){:class="flagImage"} ![Arabic]({{ site.baseurl }}/images/flags/arabic.png){:class="flagImage"} ![Vietnamese]({{ site.baseurl }}/images/flags/vietnamese.png){:class="flagImage"} ![Croatian]({{ site.baseurl }}/images/flags/croatian.png){:class="flagImage"}

VidCoder includes English, Spanish, German, French, Italian, Portuguese, Brazilian Portuguese, Dutch, Greek, Turkish, Bosnian, Korean, Czech, Polish, Indonesian, Russian, Chinese Simplified, Chinese Traditional, Japanese, Hungarian, Georgian, Basque, Arabic, Vietnamese and Croatian translations. The correct language will be selected automatically based on your OS language, or it can be selected manually.

Interested in translating VidCoder to your own language? [Help out on Crowdin](http://crowdin.net/project/vidcoder).

## Donations
VidCoder is free software. If you like VidCoder and want to express your appreciation, please [donate the GiveWell Maximum Impact Fund](https://www.givewell.org/maximum-impact-fund). It's the most effective charity in the world.

## Other info
VidCoder is built on .NET 6/WPF in C#.
It runs on the 64-bit versions of Windows 10 and 11.

The VidCoder UI (and C# interop) is written by RandomEngy.
The core encoding engine is written by the amazing HandBrake team. j45 in particular has been a huge help in getting this together.
