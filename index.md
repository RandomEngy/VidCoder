---
layout: home
title: Home
version: 3
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
{% capture betaInstallerUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ betaTag }}/VidCoder-{{ betaVersion }}-Beta-x64.exe{% endcapture %}
{% capture betaPortableUrl %}https://github.com/RandomEngy/VidCoder/releases/download/{{ betaTag }}/VidCoder-{{ betaVersion }}-Beta-x64-Portable.exe{% endcapture %}
{% capture betaNotesUrl %}https://github.com/RandomEngy/VidCoder/releases/tag/{{ tag }}{% endcapture %}

<table class="releasesTable">
<tr><td colspan="4">
<h2 class="versionHeader">Latest Stable: {{ version }}</h2>
</td></tr>
<tr>
<td><a class="button" href="{{ installerUrl }}">Download Installer</a></td><td><a href="{{ portableUrl }}" class="secondaryInstallLink">Portable (.exe)</a></td><td><a href="{{ sourceUrl }}" class="secondaryInstallLink">Source (.zip)</a></td><td><a href="{{ notesUrl }}" class="secondaryInstallLink">Release Notes</a></td>
</tr>
<tr><td colspan="4">
<h2 class="versionHeader betaVersionHeader">Latest Beta: {{ betaVersion }}</h2>
</td></tr>
<tr>
<td><a href="{{ betaInstallerUrl }}">Installer (.exe)</a></td><td><a href="{{ betaPortableUrl }}" class="secondaryInstallLink">Portable (.exe)</a></td><td><a href="{{ betaSourceUrl }}" class="secondaryInstallLink">Source (.zip)</a></td><td><a href="{{ betaNotesUrl }}" class="secondaryInstallLink">Release Notes</a></td>
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
You'll need [.NET 4](http://www.microsoft.com/downloads/details.aspx?FamilyID=e5ad0459-cbcc-4b4f-97b6-fb17111cf544) (only the client profile is needed). If you don't have it, VidCoder will prompt you to download and install it. If you're on Windows 8 you shouldn't need to install anything; it has .NET 4 pre-installed.

You *do not* need to install HandBrake for VidCoder to work.

**DVDs**
VidCoder can rip DVDs but does not defeat the CSS encryption found in most commercial DVDs. You've got several options to remove the encryption:

* [Passkey Lite](http://www.dvdfab.cn/dvd-decrypter.htm) (Free, removes encryption on the fly)
* [AnyDVD](http://www.slysoft.com/en/anydvd.html) ($89, removes encryption on the fly)
* [MakeMKV](http://www.makemkv.com/) (Free, requires copying to the hard drive before encoding)
* [DVD Decrypter](http://www.dvddecrypter.org.uk/) (Free, requires copying to the hard drive before encoding, no longer updated)

**Blu-rays**
VidCoder does not defeat AACS or BD+ Blu-ray encryption. There are a few options for removing it:

* [Passkey Lite](http://www.dvdfab.cn/dvd-decrypter.htm) (Free, removes encryption on the fly)
* [AnyDVD HD](http://www.slysoft.com/en/anydvdhd.html) ($152, removes encryption on the fly)
* [MakeMKV](http://www.makemkv.com/) (Currently free in time-limited beta, requires copying to the hard drive before encoding)

## Reporting Bugs
If you encounter any bugs with encoding, please see if they happen with the official HandBrake client as well. If the problem is reproducible on HandBrake, submit the issue to them. If it's only VidCoder that has the problem, submit it here.

Any feedback or bug reports (anything where you'd like to have a discussion or get a response) please put in Discussions or the Issue Tracker. I can't respond to reviews you write. Feel free to write reviews; just be aware that they're not a great way to ask for features or report bugs.

## Languages
![English]({{ site.baseurl }}/images/flags/english.png) ![Spanish]({{ site.baseurl }}/images/flags/spanish.png) ![German]({{ site.baseurl }}/images/flags/german.png) ![French]({{ site.baseurl }}/images/flags/french.png) ![Italian]({{ site.baseurl }}/images/flags/italian.png) ![Portuguese]({{ site.baseurl }}/images/flags/portuguese.png) ![Brazilian Portuguese]({{ site.baseurl }}/images/flags/portuguese_brazilian.png) ![Czech]({{ site.baseurl }}/images/flags/czech.png) ![Polish]({{ site.baseurl }}/images/flags/polish.png) ![Russian]({{ site.baseurl }}/images/flags/russian.png) ![Chinese Simplified]({{ site.baseurl }}/images/flags/chinese_simplified.png) ![Chinese Traditional]({{ site.baseurl }}/images/flags/chinese_traditional.png) ![Japanese]({{ site.baseurl }}/images/flags/japanese.png) ![Hungarian]({{ site.baseurl }}/images/flags/hungarian.png) ![Basque]({{ site.baseurl }}/images/flags/basque.png)
VidCoder includes English, Spanish, German, French, Italian, Portuguese, Brazilian Portuguese, Czech, Polish, Russian, Chinese Simplified, Chinese Traditional, Japanese, Hungarian and Basque translations. The correct language will be selected automatically based on your OS language, or it can be selected manually.

Interested in translating VidCoder to your own language? [Help out on Crowdin](http://crowdin.net/project/vidcoder).

## Donations
VidCoder is free software. If you like VidCoder and want to express your appreciation, please [donate to the Against Malaria Foundation](http://givewell.org/international/top-charities/AMF). It's one of the most effective charities in the world.

## Other info
VidCoder is built on .NET 4 Client Profile/WPF in C#.
It runs on Windows 7, 8, Vista, Server 2008 and Server 2012.

The VidCoder UI (and C# interop) is written by RandomEngy.
The core encoding engine is written by the amazing HandBrake team. j45 in particular has been a huge help in getting this together.
