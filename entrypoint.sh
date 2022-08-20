#!/bin/sh

echo Entrypoint running with args "$@"

# Catch SIGTERM and exit cleanly
trap "exit" TERM

# run lfmergeqm on startup to clear out any failed send/receive sessions from previous container
/lfmergeqm-background.sh & # MUST be run as a background process as it kicks off an infinite loop to run every 24 hours

CMD=${1:-"/lfmergeqm-looping.sh"}

[ -x "$CMD" ] && exec "$CMD" || exec /bin/bash
