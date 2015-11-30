---
layout: page
title: "Video Quality Options"
permalink: /Documentation/VideoQualityOptions.html
---

**The short version**: Use Constant Quality encoding. It is the best way to encode video when you do not need to strictly control output size.

## Background
You can measure the _picture quality_ of a given frame of video by determining how close the encoded frame is to the original frame. Lower quality pictures might be blurry or have blocky video artifacts.

There is a tradeoff between picture quality and amount of data used. As you apply more data to a picture, the picture quality increases. At some point you get a good looking picture and even as you apply additional data to it, it doesn't look any better.

Not all video is created equal. Video with a lot of motion or complex detail is more difficult to compress. With equal amounts of data to work with, complex video will have worse picture quality than simple video. Conversely, complex video requires more data to reach the same level of quality.

## Average Bitrate (one pass)
This is the oldest mode that many people are already aware of. You are specifying the amount of data to allow for video compression per second. Higher numbers result in higher quality. However as the encoder is working on the video, it can't know what the rest of the video is like, so it ends up spreading the data evenly across the video. This is undesirable because the simple parts of it are getting showered with data that is wasted on them. If you lowered the bitrate to make these parts efficient, the complex parts would look really bad.

## Average Bitrate (two pass)
Two-pass encoding is one way to solve this issue. It does an initial scanning pass over the whole video: just getting a good idea of what it looks like. It can figure out how to shift the data around from simple scenes to the more complex scenes while still preserving the overall average bitrate of the video. So when the second pass comes around you get an overall nicer-looking video at whatever bitrate you choose.

## Target Size
Target size is just an extra step on top of Average Bitrate. It does some calculations based on the chosen audio track and length of video to determine the appropriate video bitrate that will result in the specified target file size.

But there's still a problem with this whole approach. If you're encoding a lot of videos with the same settings, you can fall into the same trap as before. Videos which are overall very simple (like most cartoon animation) will be showered with data that they are unable to use to increase picture quality, while complex videos will not get the data required to ward off bad picture quality. Often the response to this issue is to simply raise the data rate so high that even the most complex videos will look decent. But the problem is still there: by increasing the data rate, you're wasting even _more_ data on the simple videos. What if there was a way to make every video look good but avoid wasting data? There is.

## Constant Quality
Instead of specifying a bitrate or file size, the user specifies a specific picture quality number (CRF) to shoot for. In x264 lower values mean higher qualities and higher values mean lower quality. It's a bit backwards but it's otherwise a pretty simple system. Reasonable quality targets are usually from 18-25. Here's an example of some quality levels.

![Original quality screenshot]({{ site.baseurl }}/images/firefly_original.png)
The original picture from an episode of Firefly.

![CQ 18 screenshot]({{ site.baseurl }}/images/firefly_cq18.png)
CRF 18. Nearly identical to the original picture. The full 44-minute episode ends up at 527 MB.

![CQ 20 screenshot]({{ site.baseurl }}/images/firefly_cq20.png)
CRF 20. Good quality. The episode ends up at 359 MB. This is a good tradeoff and a good place to start if you want to tinker with the value. All of the built-in presets in VidCoder use this value.

![CQ 25 screenshot]({{ site.baseurl }}/images/firefly_cq25.png)
CRF 25. The quality is a little bit worse, but the episode ends up at 171 MB.

You can experiment with a quality level easily by using the Encode Preview Clip button on the preview window in VidCoder. This will use whatever settings you have at the moment and can allow you to quickly try out different quality levels.

This way, your videos always look good and have efficient file sizes. As an added bonus, Constant Quality encoding only requires one pass. It's faster than 2-pass encoding _and_ gets better results. I recommend always using it unless you have some strict limits on how big the files can be.