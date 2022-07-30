Upgraded the timespan CLI Convert > creates mergeable files for FFMPEG

Now its possible to Convert multiple Files by only 1 encode command, with multiple defined timespans.
Only if the timespan(s) are in Range of the File the Convert will start.

https://trac.ffmpeg.org/wiki/Concatenate
Example Merge Command for FFMPEG:
ffmpeg.exe -f concat -safe 0 -y -i "mergelist.txt" -c copy -map 0 "merged.mp4"

CLI Usage - Single Timespan:
-timespan "0:00:10-0:00:20"

CLI Usage - Multiple Timespan:
-timespan "0:00:10-0:00:20;0:00:21-0:00:31"
