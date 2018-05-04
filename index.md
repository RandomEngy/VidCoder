---
layout: home
title: Home
version: 67
---

VidCoder is an open-source DVD/Blu-ray ripping and video transcoding application for Windows. It uses [HandBrake](http://handbrake.fr/) as its encoding engine.

{% for release in site.github.releases %}
{% unless release.prerelease %}
  {% assign releaseNotes = release.body %}
  {% assign sourceUrl = release.zipball_url %}
  {% assign tag = release.tag_name %}
  {% break %}
{% endunless %}
{% endfor %}
{% assign version = tag | remove: "v" %}
{% capture installerUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ tag }}/VidCoder-{{ version }}-x64.exe{% endcapture %}
{% capture portableUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ tag }}/VidCoder-{{ version }}-x64-Portable.exe{% endcapture %}
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
<td><a class="button" href="{{ installerUrl }}">Download Installer</a></td><td><a href="{{ portableUrl }}" class="secondaryInstallLink">Portable (.exe)</a></td><td><a href="{{ sourceUrl }}" class="secondaryInstallLink">Source (.zip)</a></td><td><a href="{{ notesUrl }}" class="secondaryInstallLink">Release Notes</a></td>
</tr>
</table>

<table class="releasesTable">
<tr><td colspan="4">
<h2 class="versionHeader betaVersionHeader">Latest Beta: {{ betaVersion }}</h2>
</td></tr>
<tr>
<td><a href="ms-windows-store://pdp/?productid=9NW2FKD80BXQ">Install from Microsoft Store</a></td>
<td><a href="{{ betaInstallerUrl }}" class="secondaryInstallLink">Installer (.exe)</a></td>
<td><a href="{{ betaPortableUrl }}" class="secondaryInstallLink">Portable (.exe)</a></td>
<td><a href="{{ betaSourceUrl }}" class="secondaryInstallLink">Source (.zip)</a></td>
<td><a href="{{ betaNotesUrl }}" class="secondaryInstallLink">Release Notes</a></td>
</tr>
</table>

![Main UI screenshot]({{ site.baseurl }}/images/main.png)

<iframe width="814" height="480" src="https://www.youtube.com/embed/5YEZHZghj0k" frameborder="0" allowfullscreen></iframe>

## Feature List

* Multi-threaded
* MP4, MKV containers
* H.264 encoding with [x264, the world's best video encoder](http://www.compression.ru/video/codec_comparison/h264_2012/)
* Completely integrated encoding pipeline: everything is in one process and no huge intermediate temporary files
* H.265, MPEG-4, MPEG-2, VP8, Theora video
* AAC, MP3, Vorbis, AC3, FLAC audio encoding and AAC/AC3/MP3/DTS/DTS-HD passthrough
* Target bitrate, size or quality for video
* 2-pass encoding
* Decomb, detelecine, deinterlace, rotate, reflect filters
* Batch encoding
* Instant source previews
* Creates small encoded preview clips
* Pause, resume encoding

![Preview window]({{ site.baseurl }}/images/preview.png)

![Encoding settings]({{ site.baseurl }}/images/encoding_settings.png)

## Prerequisites
You'll need [.NET 4.6](https://www.microsoft.com/en-us/download/details.aspx?id=53345). If you don't have it, VidCoder will prompt you to download and install it. If you're on Windows 10 you shouldn't need to install anything; it has .NET 4.6 pre-installed.

You *do not* need to install HandBrake for VidCoder to work.

**DVDs**
VidCoder can rip DVDs but does not defeat the CSS encryption found in most commercial DVDs. You've got several options to remove the encryption:

* [AnyDVD HD](https://www.redfox.bz/en/anydvdhd.html) ($121, removes encryption on the fly, best support for new titles)
* [Passkey Lite](http://www.dvdfab.cn/dvd-decrypter.htm) (Free, removes encryption on the fly)
* [MakeMKV](http://www.makemkv.com/) (Free, requires copying to the hard drive before encoding)
* [DVD Decrypter](http://www.dvddecrypter.org.uk/) (Free, requires copying to the hard drive before encoding, no longer updated)

**Blu-rays**
VidCoder does not defeat AACS or BD+ Blu-ray encryption. There are a couple options for removing it:

* [AnyDVD HD](https://www.redfox.bz/en/anydvdhd.html) ($121, removes encryption on the fly, best support for new titles)
* [Passkey Lite](http://www.dvdfab.cn/dvd-decrypter.htm) (Free, removes encryption on the fly)
* [MakeMKV](http://www.makemkv.com/) (Currently free in time-limited beta, requires copying to the hard drive before encoding)

## Reporting Bugs
If you encounter any bugs with encoding, please see if they happen with the official HandBrake client as well. If the problem is reproducible on HandBrake, submit the issue to them. If it's only VidCoder that has the problem, submit it here.

## Languages
![English]({{ site.baseurl }}/images/flags/english.png){:class="flagImage"} ![Spanish]({{ site.baseurl }}/images/flags/spanish.png){:class="flagImage"} ![German]({{ site.baseurl }}/images/flags/german.png){:class="flagImage"} ![French]({{ site.baseurl }}/images/flags/french.png){:class="flagImage"} ![Italian]({{ site.baseurl }}/images/flags/italian.png){:class="flagImage"} ![Portuguese]({{ site.baseurl }}/images/flags/portuguese.png){:class="flagImage"} ![Brazilian Portuguese]({{ site.baseurl }}/images/flags/portuguese_brazilian.png){:class="flagImage"} ![Dutch]({{ site.baseurl }}/images/flags/dutch.png){:class="flagImage"} ![Turkish]({{ site.baseurl }}/images/flags/turkish.png){:class="flagImage"} ![Bosnian]({{ site.baseurl }}/images/flags/bosnian.png){:class="flagImage"} ![Korean]({{ site.baseurl }}/images/flags/korean.png){:class="flagImage"} ![Czech]({{ site.baseurl }}/images/flags/czech.png){:class="flagImage"} ![Polish]({{ site.baseurl }}/images/flags/polish.png){:class="flagImage"} ![Russian]({{ site.baseurl }}/images/flags/russian.png){:class="flagImage"} ![Chinese Simplified]({{ site.baseurl }}/images/flags/chinese_simplified.png){:class="flagImage"} ![Chinese Traditional]({{ site.baseurl }}/images/flags/chinese_traditional.png){:class="flagImage"} ![Japanese]({{ site.baseurl }}/images/flags/japanese.png){:class="flagImage"} ![Hungarian]({{ site.baseurl }}/images/flags/hungarian.png){:class="flagImage"} ![Georgian]({{ site.baseurl }}/images/flags/georgian.png){:class="flagImage"} ![Basque]({{ site.baseurl }}/images/flags/basque.png){:class="flagImage"}

VidCoder includes English, Spanish, German, French, Italian, Portuguese, Brazilian Portuguese, Dutch, Turkish, Bosnian, Korean, Czech, Polish, Russian, Chinese Simplified, Chinese Traditional, Japanese, Hungarian, Georgian and Basque translations. The correct language will be selected automatically based on your OS language, or it can be selected manually.

Interested in translating VidCoder to your own language? [Help out on Crowdin](http://crowdin.net/project/vidcoder).

## Donations
VidCoder is free software. If you like VidCoder and want to express your appreciation, please [donate to the Against Malaria Foundation](http://givewell.org/international/top-charities/AMF). It's one of the most effective charities in the world.

## Other info
VidCoder is built on .NET 4.6/WPF in C#.
It runs on the 64-bit versions of Windows 7, 8, 10, Vista, Server 2008 and Server 2012.

The VidCoder UI (and C# interop) is written by RandomEngy.
The core encoding engine is written by the amazing HandBrake team. j45 in particular has been a huge help in getting this together.
