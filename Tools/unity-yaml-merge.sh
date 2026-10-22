#!/bin/sh
# Git merge driver wrapper for Unity scene/prefab/asset YAML.
#
# UnityYAMLMerge decides how to parse its inputs from the file extension, but git
# passes the three versions as extension-less temp files (.merge_file_XXXXXX).
# Called directly the tool then prints "Couldn't locate merge tool to handle
# extension" and exits 1, so every .unity merge falls back to git's text merge:
# a conflict whose working file holds only our side and no markers to warn you.
# This copies the inputs to names git can't mangle, merges, and puts the result
# where git's merge-driver contract requires it (%A, the "current" file).
#
# Registered as:
#   git config --local merge.unityyamlmerge.driver \
#     "$PWD/Tools/unity-yaml-merge.sh %O %A %B %P"

BIN="${UNITY_YAML_MERGE:-$HOME/Unity/6000.6.3f1/Editor/Data/Tools/UnityYAMLMerge}"

base=$1
ours=$2
theirs=$3
path=$4

# %P carries the real name, so the extension comes from there. Prefab variants
# are reported as "Foo.prefab(innerId)" and rare path-less merges give no
# extension at all; both cases end up as a plain scene to the tool.
ext=${path%%(*}
ext=${ext##*.}
case $ext in
    unity|prefab|asset|mat|meta) ;;
    *) ext=unity ;;
esac

[ -x "$BIN" ] || { echo "unity-yaml-merge: no tool at $BIN" >&2; exit 1; }

tmp=$(mktemp -d) || exit 1
trap 'rm -rf "$tmp"' EXIT

cp "$base"   "$tmp/base.$ext"
cp "$ours"   "$tmp/ours.$ext"
cp "$theirs" "$tmp/theirs.$ext"

"$BIN" merge -p -h "$tmp/base.$ext" "$tmp/ours.$ext" "$tmp/theirs.$ext" "$tmp/result.$ext"
rc=$?

# Exit 0 means the tool resolved every document difference; only then may we
# hand a file back to git. Anything else leaves %A untouched so git records an
# honest conflict instead of a half-merged scene.
if [ $rc -eq 0 ] && [ -s "$tmp/result.$ext" ]; then
    cp "$tmp/result.$ext" "$ours"
fi

exit $rc
