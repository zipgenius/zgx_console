#!/usr/bin/env bash
# Run as root. Use signed Ubuntu repositories with bounded mirror fallback.
set -euo pipefail
if [ "$#" -eq 0 ]; then
  echo "Usage: $0 PACKAGE..." >&2
  exit 2
fi
. /etc/os-release
suite=${VERSION_CODENAME:?Ubuntu release codename is required}
work=$(mktemp -d -t zgx-apt.XXXXXXXX)
trap 'rm -rf -- "$work"' EXIT
chmod 755 "$work"
mkdir -p "$work/lists/partial"
export DEBIAN_FRONTEND=noninteractive
options=(
  -o "Dir::Etc::sourcelist=$work/sources.list"
  -o 'Dir::Etc::sourceparts=-'
  -o "Dir::State::lists=$work/lists"
  -o 'Acquire::Retries=1'
  -o 'Acquire::http::Timeout=15'
  -o 'Acquire::https::Timeout=15'
  -o 'APT::Update::Error-Mode=any'
)
# Minimal containers can bootstrap HTTPS using the trusted CA bundle of the runner.
if [ -n "${ZG_APT_CA_BUNDLE:-}" ]; then
  if [ ! -r "$ZG_APT_CA_BUNDLE" ]; then
    echo "Cannot read CA bundle: $ZG_APT_CA_BUNDLE" >&2
    exit 2
  fi
  options+=(-o "Acquire::https::CaInfo=$ZG_APT_CA_BUNDLE")
fi
# Allow a local mirror list for infrastructure configuration and validation.
mirrors=${ZG_UBUNTU_MIRRORS:-'https://mirrors.mit.edu/ubuntu
https://mirrors.edge.kernel.org/ubuntu
https://archive.ubuntu.com/ubuntu'}
while IFS= read -r mirror; do
  [ -n "$mirror" ] || continue
  case "$mirror" in https://*) ;; *) echo "Expected an HTTPS mirror: $mirror" >&2; exit 2 ;; esac
  : > "$work/sources.list"
  for pocket in "$suite" "$suite-updates" "$suite-security"; do
    printf 'deb [signed-by=/usr/share/keyrings/ubuntu-archive-keyring.gpg] %s %s main restricted universe multiverse\n' "$mirror" "$pocket" >> "$work/sources.list"
  done
  echo "Installing Ubuntu tools from $mirror ($suite)"
  if apt-get "${options[@]}" update && apt-get "${options[@]}" install -y --no-install-recommends "$@"; then
    exit 0
  fi
  echo "Mirror failed: $mirror; trying the next mirror." >&2
done <<< "$mirrors"
echo 'Unable to install build tools from any configured Ubuntu mirror.' >&2
exit 1
