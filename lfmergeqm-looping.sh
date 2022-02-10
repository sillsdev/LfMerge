#!/bin/sh

# Run lfmergeqm every time a file is created in the sync queue

# This is expected to run as the CMD, launched by the entry point.

while inotifywait -e create /var/lib/languageforge/lexicon/sendreceive/syncqueue; do
  sudo -u www-data lfmergeqm
done
