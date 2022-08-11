#########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

ROOT_DIR := $(shell dirname $(realpath $(firstword $(MAKEFILE_LIST))))
BIN_DIR := $(ROOT_DIR)/bin
SRC_DIR := $(ROOT_DIR)/src
TF_DIR := $(ROOT_DIR)/terraform

CORES ?= $(shell sysctl -n hw.ncpu || echo 4)

.DEFAULT_GOAL := list
.ONESHELL:

###############################################################################
#                                  Templates                                  #
###############################################################################
update-templates:
	@cd $(ROOT_DIR)
	omg update
	omg proj
	omg env

###############################################################################
#                                   Build                                     #
###############################################################################
clean:
	$(BIN_DIR)/clean

build:
	$(BIN_DIR)/build

test:
	$(BIN_DIR)/test

###############################################################################
#                                 Terraform                                   #
###############################################################################
tf-ci: tf-ci-init
	@cd $(TF_DIR)/ci
	terraform plan

tf-ci-init:
	@cd $(TF_DIR)/ci
	terraform init

tf-ci-apply: tf-ci-init
	@cd $(TF_DIR)/ci
	terraform apply -auto-approve

tf-ci-upgrade:
	@cd $(TF_DIR)/ci
	terraform init -upgrade

tf-ci-output:
	@cd $(TF_DIR)/ci
	@terraform output -json

###############################################################################
#                                 Utilities                                   #
###############################################################################

# https://stackoverflow.com/a/26339924/11547115
.PHONY: list
list:
	@$(MAKE) -pRrq -f $(lastword $(MAKEFILE_LIST)) : 2>/dev/null | awk -v RS= -F: '/^# File/,/^# Finished Make data base/ {if ($$1 !~ "^[#.]") {print $$1}}' | sort | egrep -v -e '^[^[:alnum:]]' -e '^$@$$'
