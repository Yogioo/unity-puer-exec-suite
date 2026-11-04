#!/bin/sh
# Install com.yogioo.puer-exec-suite into a Unity project as an embedded package.
#
#   ./install.sh /path/to/UnityProject            install (project .gitignore updated)
#   ./install.sh /path/to/UnityProject --enable   install + per-machine opt-in marker
#   ./install.sh /path/to/UnityProject --update   git pull inside the installed package
#   ./install.sh /path/to/UnityProject --remove   uninstall
#   --source <git-url|dir>   install from somewhere other than the default public repo
#   --force                  reinstall over an existing folder
#   --no-ignore               leave the project .gitignore alone
#
# NOTE: the bundled native plugins are Windows Editor only (x86_64).
set -e

PKG=com.yogioo.puer-exec-suite
SOURCE="https://github.com/Yogioo/unity-puer-exec-suite.git"
PROJECT=""
ENABLE=0
UPDATE=0
REMOVE=0
FORCE=0
NOIGNORE=0

while [ $# -gt 0 ]; do
  case "$1" in
    --source) SOURCE=$2; shift 2 ;;
    --enable) ENABLE=1; shift ;;
    --update) UPDATE=1; shift ;;
    --remove) REMOVE=1; shift ;;
    --force) FORCE=1; shift ;;
    --no-ignore) NOIGNORE=1; shift ;;
    -h|--help) sed -n '2,14p' "$0"; exit 0 ;;
    -*) echo "unknown option: $1" >&2; exit 2 ;;
    *) PROJECT=$1; shift ;;
  esac
done

[ -n "$PROJECT" ] || { echo "usage: $0 <unity-project> [--enable|--update|--remove]" >&2; exit 2; }
[ -d "$PROJECT/Assets" ] || { echo "not a Unity project (no Assets/): $PROJECT" >&2; exit 2; }
PROJECT=$(cd "$PROJECT" && pwd)
TARGET="$PROJECT/Packages/$PKG"
FLAG="$PROJECT/UserSettings/PuerExecSuite/enabled"
ENTRY="/Packages/$PKG/"

echo "project : $PROJECT"
echo "package : $TARGET"
echo

add_ignore() {
  [ "$NOIGNORE" = 1 ] && return 0
  touch "$PROJECT/.gitignore"
  grep -qxF "$ENTRY" "$PROJECT/.gitignore" && return 0
  if [ -s "$PROJECT/.gitignore" ]; then
    printf '\n# puer-exec suite: per-machine, default off\n%s\n' "$ENTRY" >> "$PROJECT/.gitignore"
  else
    printf '# puer-exec suite: per-machine, default off\n%s\n' "$ENTRY" >> "$PROJECT/.gitignore"
  fi
  echo "  ignore  $ENTRY -> .gitignore"
}

drop_ignore() {
  [ -f "$PROJECT/.gitignore" ] || return 0
  if grep -qxF "$ENTRY" "$PROJECT/.gitignore"; then
    grep -vxF "$ENTRY" "$PROJECT/.gitignore" \
      | grep -vxF '# puer-exec suite: per-machine, default off' \
      > "$PROJECT/.gitignore.tmp"
    mv "$PROJECT/.gitignore.tmp" "$PROJECT/.gitignore"
    echo "  ignore  $ENTRY removed from .gitignore"
  fi
}

if [ "$REMOVE" = 1 ]; then
  rm -rf "$TARGET"
  echo "  removed package folder"
  rm -f "$FLAG"
  echo "  removed opt-in marker"
  drop_ignore
  echo
  echo "Done. Refocus the Unity Editor to unload the package."
  exit 0
fi

if [ "$UPDATE" = 1 ]; then
  [ -d "$TARGET/.git" ] || { echo "no git checkout at $TARGET; reinstall instead" >&2; exit 2; }
  git -C "$TARGET" pull --ff-only
  echo "  updated"
  exit 0
fi

if [ -e "$TARGET" ]; then
  [ "$FORCE" = 1 ] || { echo "$TARGET already exists (use --update / --force / --remove)" >&2; exit 2; }
  rm -rf "$TARGET"
  echo "  removed previous install (--force)"
fi

mkdir -p "$PROJECT/Packages"
case "$SOURCE" in
  http*|git@*|*.git)
    git clone --depth 1 "$SOURCE" "$TARGET"
    ;;
  *)
    [ -d "$SOURCE" ] || { echo "source not found: $SOURCE" >&2; exit 2; }
    mkdir -p "$TARGET"
    (cd "$SOURCE" && tar -cf - --exclude=.git .) | (cd "$TARGET" && tar -xf -)
    ;;
esac
echo "  install $TARGET"

add_ignore

if [ "$ENABLE" = 1 ]; then
  mkdir -p "$(dirname "$FLAG")"
  printf 'enabled\n' > "$FLAG"
  echo "  enable  $FLAG"
fi

cat <<EOF

Done. Next steps:
  1. Refocus or reopen the Unity Editor; the package compiles editor-only.
  2. Off by default. To drive this Editor from a CLI either:
       a) launch with the activation switch:
          Unity -projectPath "$PROJECT" -unityPuerExecControl
       b) or opt in on this machine (persists across Editor restarts):
          menu Tools/Puer Exec Suite/Enable On This Machine
     CLI: $TARGET/Vendor/com.txcombo.unity-puer-exec/CLI~/unity-puer-exec.exe
EOF
