#!/bin/sh

now=`date +"%Y%m%d-%H%M"`

mv logs logs-$now
mkdir logs
