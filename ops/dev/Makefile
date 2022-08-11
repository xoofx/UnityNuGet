#########################################################################
#
#                 -- Generated with omgcmd --
#      (do not edit unless you know what you're doing)
#
#########################################################################

ROOT_DIR:=$(shell dirname $(realpath $(firstword $(MAKEFILE_LIST))))
TF_DIR := $(ROOT_DIR)/../../terraform
CORES ?= $(shell sysctl -n hw.ncpu || echo 4)

.DEFAULT_GOAL := list
.ONESHELL:

ENVIRONMENT_MODULES := dev
define TERRAFORM
tf-$1:
	@cd $(TF_DIR)/environments/$1
	tf $1 plan

#tf-$1-destroy:
#  @cd $(TF_DIR)/environments/$1
#  tf $1 destroy

tf-$1-init:
	@cd $(TF_DIR)/environments/$1
	tf $1 init -input=false

tf-$1-upgrade:
	@cd $(TF_DIR)/environments/$1
	tf $1 init -upgrade

tf-$1-apply:
	@cd $(TF_DIR)/environments/$1
	tf $1 apply -auto-approve

tf-$1-bootstrap: tf-$1-upgrade
	@cd $(TF_DIR)/environments/$1
	tf $1 apply -target module.locals -auto-approve

tf-$1-output:
	@cd $(TF_DIR)/environments/$1
	@terraform output -json

tf-$1-validate:
	@cd $(TF_DIR)/environments/$1
	@tf $1 validate
endef
$(foreach component,$(ENVIRONMENT_MODULES),$(eval $(call TERRAFORM,$(component))))

###############################################################################
#                                 Utilities                                   #
###############################################################################

# https://stackoverflow.com/a/26339924/11547115
.PHONY: list
list:
	@$(MAKE) -pRrq -f $(lastword $(MAKEFILE_LIST)) : 2>/dev/null | awk -v RS= -F: '/^# File/,/^# Finished Make data base/ {if ($$1 !~ "^[#.]") {print $$1}}' | sort | egrep -v -e '^[^[:alnum:]]' -e '^$@$$'
