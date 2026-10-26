#!/usr/bin/env bash
# Backward-compatible entry point; accepts an optional Unity test filter.
exec bash "$(dirname "$0")/tests.sh" play "$@"
