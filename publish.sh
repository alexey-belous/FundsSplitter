#!/bin/bash

rm -rf ./publish
mkdir ./publish
mkdir ./publish/app
dotnet publish ./src/FundsSplitter.App -o ./../../publish/app -r linux-x64 --self-contained true

cp ./docker-compose.prod.yml ./publish/docker-compose.yml
rm -rf ./publish/app/config.json