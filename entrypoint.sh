#!/bin/sh

echo Entrypoint running with args "$@"

# Catch SIGTERM and exit cleanly
trap "exit" TERM

# rsyslog needs to run so that lfmerge can log to /var/log/syslog
/etc/init.d/rsyslog start

# Ensure /var/log/syslog exists so tail -f /var/log/syslog will run
logger "Starting container..."
# echo /var/log/syslog to container stdout so it shows up in `kubectl logs`
# First waiting for syslog to exist (race condition: `logger` can return before syslog file is created)
until [ -r /var/log/syslog ]; do
  sleep 0.2
done
tail -f /var/log/syslog &

# run lfmergeqm on startup to clear out any failed send/receive sessions from previous container
/lfmergeqm-background.sh & # MUST be run as a background process as it kicks off an infinite loop to run every 24 hours

CMD=${1:-"/lfmergeqm-looping.sh"}

[ -x "$CMD" ] && exec "$CMD" || exec /bin/bash
