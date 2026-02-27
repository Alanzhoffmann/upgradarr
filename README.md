# Upgradarr

Inspired by Huntarr, but wanting to put my own rules on missing/upgrade search and download cleanup, I decided to write my own little script to interface with Sonarr and Radarr and stop me from having to manually fix these issues.
This is a simple project I put together in very little time just to play around with docker support in .NET, and to sharpen my know-how.

## What does this do? 

Upgradarr is built to: 
- Search for missing monitored movies and TV shows;
- Upgrade existing monitored movies and TV shows;
- Clean up stalled and stuck downloads, blocklist and search for replacements.

## What this doesn't do (yet) 

What I'm still planning on implementing:
- Multiple Sonarr/Radarr instances (anime, 4K/1080p splits);
- Stop searching on cutoff met;
- UI for monitoring and configuration;
- Better versioning.

## Wait, do I need this?

Honestly? Probably not. Sonarr and Radarr do a great job of monitoring the RSS feeds for any new downloads that become available for your monitored titles. This will not replace that. You want to use Upgradarr if you:
- Keep trying to find episodes for that one TV show and they're always stalling;
- Already have a library, but would want to check if any of your titles has a better version out there;
- Is tired of manually getting rid of downloads and you're not sure whether you'll find it when searching for a whole season or just a single episode;
- Keep having file imports failing for compressed data and weird file extensions.

## How to use

To use this, add the following to your docker compose:
```
services:
  upgradarr:
    image: ghcr.io/alanzhoffmann/upgradarr:unstable
    container_name: upgradarr
    user: ${PUID}:${PGID}
    environment:
      - Sonarr__ApiKey=${SONARR_KEY}
      - Radarr__ApiKey=${RADARR_KEY}
    volumes:
      - /path/to/config:/config
#    ports:
#      - 1234:8080 # You don't need to expose anything
```

## Are there any drawbacks?

Yes. Upgradarr can be very eager to get rid of downloads, so if you have certain rules on seeding for your indexes, you might want to tweak the settings a little bit or skip this tool altogether. You might also see an increase in downloads for titles that can be just a sidegrade at best, or can replace your carefully chosen media. In these cases, I recommend unmonitoring the title/season/episode so that it doesn't try to replace your media (it might still try if another episode of the same season or another season of the same show is monitored)
