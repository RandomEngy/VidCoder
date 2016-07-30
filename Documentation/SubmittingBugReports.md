---
layout: page
title: "Submitting Bug Reports"
permalink: /Documentation/SubmittingBugReports.html
---

## General problems

VidCoder uses the HandBrake core APIs to do the heavy lifting for scanning and encoding video files. VidCoder itself is responsible for:

* The user interface
* Passing parameters to the HandBrake engine
* Managing presets
* Managing the encode queue

This means that if you had a problem with the actual encode result like a blurry image or problems playing on a certain device, you would have to ask the HandBrake people for help. They don't watch the forums on this site. Just note that before you ask for help there, you should probably confirm that it's a HandBrake issue by using their official UI: to ensure that it's not a problem with VidCoder passing parameters to it. Also be sure to have a HandBrake encode log prepared if you take a problem to them. Problems with the user interface I'd be glad to handle.

## Crashes

Crashes will generally happen in two different places: In my code (VidCoder-specific) and in the HandBrake core.

**In VidCoder**
When it crashes in a VidCoder-specific area, you should get a dialog box like this:

![Exception screenshot]({{ site.baseurl }}/images/exception.png)

Those are very good as they tell me exactly what's wrong and when they're reported I'm able to fix them in the next release almost all of the time. Though it will help me diagnose the issue faster if you tell me what you were doing at the time of the crash.

**In HandBrake Core**
There's another type of crash that doesn't give those nice details, and that usually means it crashed down somewhere in the unmanaged HandBrake core. For these, a good thing to find out would be: Does it also crash in the [lastest nighly of the official HandBrake GUI](https://handbrake.fr/nightly.php)? If it does you can report the problem to them for them to fix. If it doesn't, then there might be something wrong on my end. In that case since I don't have any information about why the crash happened, I'll need more information.

## Helpful information
1. The file you're trying to encode (or part of the file). You can use [pCloud Transfer](https://transfer.pcloud.com/) and send to david.rickard@gmail.com
2. The exported preset that you're using (Tools -> Export Preset)
3. An encode log (Windows -> Log -> Copy, then post to [Pastebin](http://pastebin.com/))

## Where to report

[Create a new issue on GitHub](https://github.com/RandomEngy/VidCoder/issues/new) 