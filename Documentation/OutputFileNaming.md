---
layout: page
title: "Output File Naming"
permalink: /Documentation/OutputFileNaming.html
---

There are 3 choices of how to name your output files.

## Auto-name with default format
This is the option used if no other action is taken. Names will look something like this:
Source Name - Title 3 - Chapters 5-8.mp4

## Auto-name with custom format
You can specify your own format here. There are a number of placeholders you can put into the format:

**{source}** | The name of the source video. For Blu-rays or DVDs, this will be the name of the disc. For files, this will be the name of the file without the extension.
**{title}** | The title number on the DVD or Blu-ray. Use {title:2} to add leading zeroes to make the title number 2 characters long.
**{range}** | The range of chapters/seconds/frames used, such as "3-6" or "8.4-15.9".
**{preset}** | The name of the preset used to encode the file.
**{date}** | The date the file was added to the queue.
**{time}** | The time of day the file was added to the queue.
**{quality}** | The quality number used on this file. Could be a size, bitrate or quality target, depending on what was targeted for the video encode.
**{parent}** | The parent folder of the source file. Use {parent:2} to get the grandparent. Will be empty if the parent does not exist.
**{titleduration}** | The length of the title being encoded.

&nbsp;  
For example, a format of
{source} - Title {title:2} - CRF {quality} ({date} {time})

could result in a file:
MySource - Title 04 - CRF 20 (2011-01-15 16.27.12).mkv

You can include "\" in the format string to create subdirectories. For example:
{parent}\{source}-{title}

## Manually name
You can click on the output path on the main form and edit the name. In this case the auto-naming will be overridden.