#!/bin/sh

# Run lfmergeqm every time a file is created in the sync queue
# We use the close_write event because the create event can be fired before the file's contents are written, and then `lfmergeqm` can run too early

# This is expected to run as the CMD, launched by the entry point.

while inotifywait -e close_write /var/lib/languageforge/lexicon/sendreceive/syncqueue; do
  sudo -u www-data lfmergeqm
done
