#!/bin/bash
# If this script fails because Ansible can't verify the host fingerprint, run it as
# ANSIBLE_HOST_KEY_CHECKING=False ./deploy.sh build

if [ $# -le 0 ];
then
	echo 'Please specify server type: either "build" or "production"'
	exit 1
fi

ansible-playbook -i hosts lfmerge.yaml --extra-vars "target=$1" --ask-become-pass
