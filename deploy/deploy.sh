#!/bin/bash

if [ $# -le 0 ];
then
	echo 'Please specify server type: either "build" or "production"'
	exit 1
fi

ansible-playbook -i hosts lfmerge.yaml --extra-vars "target=$1" --ask-become-pass
