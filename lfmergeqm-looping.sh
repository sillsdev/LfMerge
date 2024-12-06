#!/bin/sh

# Catch SIGTERM and exit cleanly
trap "exit" TERM
# This is also handled in entrypoint.sh, but since entrypoint.sh uses exec to run this script, we need to handle SIGTERM here as well

# Run lfmergeqm every time a file is created in the sync queue
# We use the close_write event because the create event can be fired before the file's contents are written, and then `lfmergeqm` can run too early

# This is expected to run as the CMD, launched by the entry point.

while inotifywait -e close_write /var/lib/languageforge/lexicon/sendreceive/syncqueue; do
  sudo -H --preserve-env=CHORUS_HG_EXE -u www-data lfmergeqm
  # Run it again just to ensure that any initial clones that missed a race condition have a chance to get noticed
  sudo -H --preserve-env=CHORUS_HG_EXE -u www-data lfmergeqm
done
