# upgradarr
Trying to put together something like huntarr but in .net


To setup use this:
```
services:
  upgradarr:
    image: ghcr.io/alanzhoffmann/upgradarr:main
    container_name: upgradarr
    user: ${PUID}:${PGID}
    environment:
      - Sonarr__ApiKey=${SONARR_KEY}
      - Radarr__ApiKey=${RADARR_KEY}
    volumes:
      - /path/to/config:/config
    ports:
      - 1234:8080
```