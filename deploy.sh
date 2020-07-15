#!/bin/bash

eval "$(ssh-agent -s)" # start ssh-agent cache
chmod 600 /root/cert
ssh-add /root/cert
mkdir -p ~/.ssh
ssh-keyscan -p $PROD_PORT $PROD_HOSTNAME >> ~/.ssh/known_hosts

tar -czf ./package.tar.gz ./publish/**
scp -i /root/cert -P $PROD_PORT ./package.tar.gz $PROD_USERNAME@$PROD_HOSTNAME:/tmp/package.tar.gz