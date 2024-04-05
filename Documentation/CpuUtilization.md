---
layout: page
title: "CPU Utilization"
permalink: /Documentation/CpuUtilization.html
---

A common complaint about VidCoder goes something like **"I'm encoding but it's only using 30% of my CPU."**

There are a few reasons why you might see lower CPU utilization:

* CPU Throttling may be enabled in Global Options -> Process. Make sure Allowed Cores is at 100%.
* You might be using a GPU-based encoder like NVEnc, and the CPU doesn't have a lot of work it needs to do.
* You might be using a filter that runs single-threaded in the CPU that is bottlenecking the encode. To check if this is the case, disable any filters or modifications on the encode:
  * On the Video Filters tab - disable all filters.
  * On the Sizing tab - disableto cropping, padding and resizing. Resizing should be Automatic, with no max size.
  * Check the Video Encoding tab: Framerate should be Same as Source, Variable Framerate.
* You might have a machine that has so many cores, the multi-threaded encoder cannot make use of them all.

In some cases you can increase overall encoding throughput by allowing simultaneous encodes: Global options -> Process -> Maximum simultaneous encoding jobs.