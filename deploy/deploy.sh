#!/bin/bash

if [ $# -le 0 ];
then
    echo 'Please specify server type: either "build" or "production"'
fi

ansible-playbook -i hosts lfmerge.yaml --extra-vars "target=$1"
