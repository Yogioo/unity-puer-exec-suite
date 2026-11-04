#!/bin/sh
# Install com.yogioo.puer-exec-suite into a Unity project as a local, gitignored tree.
#
#   ./install.sh /path/to/UnityProject                 install into Assets/Editor/PuerExecSuite (default)
#   ./install.sh /path/to/UnityProject --location packages   install as a UPM embedded package instead
#   ./install.sh /path/to/UnityProject --enable        install + per-machine opt-in marker
#   ./install.sh /path/to/UnityProject --enable-project      install + shared opt-in marker (commit it)
#   ./install.sh /path/to/UnityProject --update        git pull inside the installed tree
#   ./install.sh /path/to/UnityProject --remove        uninstall from both locations
#   --source <git-url|dir>   install from somewhere other than the default public repo
#   --force                  reinstall over an existing tree
#   --no-ignore              leave the project .gitignore alone
#
# Location matters for git hygiene:
#   assets   code is compiled as project content; the Package Manager never sees it, so
#            Packages/packages-lock.json is never touched.
#   packages a UPM embedded package; UPM then records (and on other machines prunes)
#            a packages-lock.json entry, dirtying the lock on whichever side lacks it.
#
# NOTE: the bundled native plugins are Windows Editor only (x86_64).
set -e

PKG=com.yogioo.puer-exec-suite
ASSET_REL=Assets/Editor/PuerExecSuite
PACKAGE_REL=Packages/$PKG
SOURCE="https://github.com/Yogioo/unity-puer-exec-suite.git"
PROJECT=""
LOCATION=assets
ENABLE=0
ENABLE_PROJECT=0
UPDATE=0
REMOVE=0
FORCE=0
NOIGNORE=0

while [ $# -gt 0 ]; do
  case "$1" in
    --location) LOCATION=$2; shift 2 ;;
    --source) SOURCE=$2; shift 2 ;;
    --enable) ENABLE=1; shift ;;
    --enable-project) ENABLE_PROJECT=1; shift ;;
    --update) UPDATE=1; shift ;;
    --remove) REMOVE=1; shift ;;
    --force) FORCE=1; shift ;;
    --no-ignore) NOIGNORE=1; shift ;;
    -h|--help) sed -n '2,20p' "$0"; exit 0 ;;
    -*) echo "unknown option: $1" >&2; exit 2 ;;
    *) PROJECT=$1; shift ;;
  esac
done

case "$LOCATION" in
  assets) REL_TARGET=$ASSET_REL ;;
  packages) REL_TARGET=$PACKAGE_REL ;;
  *) echo "--location must be assets or packages" >&2; exit 2 ;;
esac

[ -n "$PROJECT" ] || { echo "usage: $0 <unity-project> [--location assets|packages] [--enable|--enable-project|--update|--remove]" >&2; exit 2; }
[ -d "$PROJECT/Assets" ] || { echo "not a Unity project (no Assets/): $PROJECT" >&2; exit 2; }
PROJECT=$(cd "$PROJECT" && pwd)
TARGET="$PROJECT/$REL_TARGET"
OTHER_TARGET="$PROJECT/$( [ "$LOCATION" = packages ] && echo "$ASSET_REL" || echo "$PACKAGE_REL" )"
OTHER_REL=$( [ "$LOCATION" = packages ] && echo "$ASSET_REL" || echo "$PACKAGE_REL" )
FLAG="$PROJECT/UserSettings/PuerExecSuite/enabled"
FLAG_DISABLED="$PROJECT/UserSettings/PuerExecSuite/disabled"
PROJECT_FLAG="$PROJECT/ProjectSettings/PuerExecSuite/enabled"

echo "project : $PROJECT"
echo "location: $LOCATION"
echo "target  : $TARGET"
echo

add_ignore() {
  [ "$NOIGNORE" = 1 ] && return 0
  touch "$PROJECT/.gitignore"
  changed=0
  for rel in "$@"; do
    for entry in "/$rel/" "/$rel.meta"; do
      if ! grep -qxF "$entry" "$PROJECT/.gitignore"; then
        if grep -qxF '# puer-exec suite: per-machine, default off' "$PROJECT/.gitignore"; then
          printf '%s
' "$entry" >> "$PROJECT/.gitignore"
        elif [ -s "$PROJECT/.gitignore" ]; then
          printf '\n# puer-exec suite: per-machine, default off\n%s\n' "$entry" >> "$PROJECT/.gitignore"
        else
          printf '# puer-exec suite: per-machine, default off\n%s\n' "$entry" >> "$PROJECT/.gitignore"
        fi
        echo "  ignore  $entry -> .gitignore"
        changed=1
      fi
    done
  done
  return 0
}

drop_ignore() {
  [ -f "$PROJECT/.gitignore" ] || return 0
  for rel in "$@"; do
    for entry in "/$rel/" "/$rel.meta"; do
      if grep -qxF "$entry" "$PROJECT/.gitignore"; then
        grep -vxF "$entry" "$PROJECT/.gitignore" \
          | grep -vxF '# puer-exec suite: per-machine, default off' \
          > "$PROJECT/.gitignore.tmp"
        mv "$PROJECT/.gitignore.tmp" "$PROJECT/.gitignore"
        echo "  ignore  $entry removed from .gitignore"
      fi
    done
  done
}

write_markers() {
  if [ "$ENABLE" = 1 ]; then
    mkdir -p "$(dirname "$FLAG")"
    printf 'enabled\n' > "$FLAG"
    rm -f "$FLAG_DISABLED"
    echo "  enable  $FLAG (this machine)"
  fi
  if [ "$ENABLE_PROJECT" = 1 ]; then
    mkdir -p "$(dirname "$PROJECT_FLAG")"
    printf 'enabled\n' > "$PROJECT_FLAG"
    echo "  enable  $PROJECT_FLAG (commit this to share with the team)"
  fi
}

if [ "$REMOVE" = 1 ]; then
  for rel in "$ASSET_REL" "$PACKAGE_REL"; do
    if [ -e "$PROJECT/$rel" ]; then
      rm -rf "$PROJECT/$rel"
      echo "  removed $rel"
      drop_ignore "$rel"
    fi
  done
  rm -f "$FLAG" "$FLAG_DISABLED"
  echo "  removed per-machine markers"
  if [ -f "$PROJECT_FLAG" ]; then
    echo "  kept ProjectSettings/PuerExecSuite/enabled (shared team decision)"
  fi
  echo
  echo "Done. Refocus the Unity Editor to unload the module."
  exit 0
fi

if [ "$UPDATE" = 1 ]; then
  for rel in "$ASSET_REL" "$PACKAGE_REL"; do
    if [ -d "$PROJECT/$rel/.git" ]; then
      for attempt in 1 2 3 4; do
        git -C "$PROJECT/$rel" pull --ff-only && break
        [ "$attempt" = 4 ] || { echo "  pull attempt $attempt failed, retrying..." >&2; sleep 3; }
      done
      echo "  updated $rel"
    fi
  done
  exit 0
fi

if [ -e "$OTHER_TARGET" ]; then
  echo "$OTHER_REL is also installed; run --remove first (duplicate assembly names otherwise)" >&2
  exit 2
fi

if [ -e "$TARGET" ]; then
  if [ "$FORCE" != 1 ]; then
    if [ "$ENABLE" = 1 ] || [ "$ENABLE_PROJECT" = 1 ]; then
      echo "  already installed; updating markers only"
      add_ignore "$REL_TARGET"
      write_markers
      exit 0
    fi
    echo "  already installed; verifying .gitignore"
    add_ignore "$REL_TARGET"
    exit 0
    echo "$TARGET already exists (use --update / --force / --remove)" >&2
    exit 2
  fi
  rm -rf "$TARGET"
  echo "  removed previous install (--force)"
fi

mkdir -p "$(dirname "$TARGET")"
case "$SOURCE" in
  http*|git@*|*.git)
    # github.com 在国内常见「连上就断」，重试几次比换配置有效
    ok=0
    for attempt in 1 2 3 4; do
      git clone --depth 1 "$SOURCE" "$TARGET" && { ok=1; break; }
      rm -rf "$TARGET"
      [ "$attempt" = 4 ] || { echo "  clone attempt $attempt failed, retrying..." >&2; sleep 3; }
    done
    if [ "$ok" != 1 ]; then
      echo >&2
      echo "clone failed. If https://github.com is blocked on this network, retry with:" >&2
      echo "  --source git@github.com:Yogioo/unity-puer-exec-suite.git" >&2
      echo "  --source https://ghproxy.net/https://github.com/Yogioo/unity-puer-exec-suite.git" >&2
      echo "or download the zip and point --source at the extracted folder." >&2
      exit 1
    fi
    ;;
  *)
    [ -d "$SOURCE" ] || { echo "source not found: $SOURCE" >&2; exit 2; }
    mkdir -p "$TARGET"
    (cd "$SOURCE" && tar -cf - --exclude=.git .) | (cd "$TARGET" && tar -xf -)
    ;;
esac
echo "  install $TARGET"

add_ignore "$REL_TARGET"

write_markers

cat <<EOF

Done. Next steps:
  1. Refocus or reopen the Unity Editor; the module compiles editor-only.
  2. Off by default. Enable once, then it auto-activates on every Editor start:
       menu Tools/Puer Exec Suite/Enable On This Machine    (this machine only)
       menu Tools/Puer Exec Suite/Enable For This Project   (committed, whole team)
     Per-process, for an isolated log:
       Unity -projectPath "$PROJECT" -unityPuerExecControl
     CLI: $TARGET/Vendor/com.txcombo.unity-puer-exec/CLI~/unity-puer-exec.exe
EOF
