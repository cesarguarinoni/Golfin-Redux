#!/usr/bin/env bash
# GOLFIN Architecture Audit Generator (bash, mac/linux)
# Mirror of generate_audit.ps1 for non-Windows platforms.
# Usage: bash Docs/generate_audit.sh > Docs/ARCHITECTURE_AUDIT.md
set -u
# Don't use -e: many greps below intentionally match nothing per file.

SCRIPTS_DIR="Assets/Scripts"
DATA_DIR="Assets/Data"

echo "# Architecture Audit"
echo
echo "> Auto-generated $(date '+%Y-%m-%d %H:%M'). Do not edit manually."
echo

echo "## File Tree (Scripts)"
echo
echo '```'
find "$SCRIPTS_DIR" -type f -name "*.cs" 2>/dev/null | sort
echo '```'
echo

echo "## File Tree (Data)"
echo
echo '```'
[ -d "$DATA_DIR" ] && find "$DATA_DIR" -type f 2>/dev/null | sort
echo '```'
echo

echo "## MonoBehaviours"
echo
echo "| Class | File | Singleton | Key Interfaces |"
echo "|---|---|---|---|"

while IFS= read -r file; do
  has_instance=""
  grep -q "Instance" "$file" && has_instance="Yes"
  grep -nE 'class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*:[[:space:]]*[^{]+' "$file" | while IFS=: read -r _ line; do
    if echo "$line" | grep -q "MonoBehaviour"; then
      cls=$(echo "$line" | sed -nE 's/.*class[[:space:]]+([A-Za-z_][A-Za-z0-9_]*).*/\1/p')
      inh=$(echo "$line" | sed -nE 's/.*class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*:[[:space:]]*([^{]+).*/\1/p')
      ifaces=$(echo "$inh" | sed -E 's/MonoBehaviour//; s/^[[:space:]]*,?[[:space:]]*//; s/[[:space:]]*,[[:space:]]*$//; s/[[:space:]]+$//')
      echo "| $cls | $file | $has_instance | $ifaces |"
    fi
  done
done < <(find "$SCRIPTS_DIR" -type f -name "*.cs" 2>/dev/null | sort)
echo

echo "## Singletons"
echo
while IFS= read -r file; do
  if grep -qE 'static[[:space:]]+[A-Za-z_][A-Za-z0-9_<>]*[[:space:]]+Instance' "$file"; then
    cls=$(grep -oE 'class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "$file" | head -1 | awk '{print $2}')
    [ -z "$cls" ] && cls="Unknown"
    ddol=""
    grep -q "DontDestroyOnLoad" "$file" && ddol="(DontDestroyOnLoad)"
    echo "- **$cls** ($file) $ddol"
  fi
done < <(find "$SCRIPTS_DIR" -type f -name "*.cs" 2>/dev/null | sort)
echo

echo "## Events (Action delegates)"
echo
echo "| Class | Event |"
echo "|---|---|"
while IFS= read -r file; do
  cls=$(grep -oE 'class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "$file" | head -1 | awk '{print $2}')
  [ -z "$cls" ] && cls="Unknown"
  grep -nE 'event[[:space:]]+.*Action' "$file" | while IFS=: read -r _ line; do
    line_trim=$(echo "$line" | sed -E 's/^[[:space:]]+//;s/[[:space:]]+$//')
    echo "| $cls | \`$line_trim\` |"
  done
done < <(find "$SCRIPTS_DIR" -type f -name "*.cs" 2>/dev/null | sort)
echo

echo "## Serialized Fields Summary"
echo
echo "| Class | File | SerializeField Count |"
echo "|---|---|---|"
while IFS= read -r file; do
  count=$(grep -cE 'SerializeField' "$file" || true)
  if [ "${count:-0}" -gt 0 ]; then
    cls=$(grep -oE 'class[[:space:]]+[A-Za-z_][A-Za-z0-9_]*' "$file" | head -1 | awk '{print $2}')
    [ -z "$cls" ] && cls="Unknown"
    echo "| $cls | $file | $count |"
  fi
done < <(find "$SCRIPTS_DIR" -type f -name "*.cs" 2>/dev/null | sort)
echo

echo "## CSV Data Files"
echo
if [ -d "$DATA_DIR" ]; then
  while IFS= read -r csv; do
    echo "### $(basename "$csv")"
    echo '```'
    head -2 "$csv"
    echo '```'
    rows=$(wc -l < "$csv" | tr -d ' ')
    echo "($rows rows)"
    echo
  done < <(find "$DATA_DIR" -type f -name "*.csv" 2>/dev/null | sort)
fi

echo "## Quick Health"
echo
echo "### Potential Missing Methods on CharacterManager"
echo '```'

CALLED=$(grep -rohE 'CharacterManager\.Instance\.[A-Za-z_][A-Za-z0-9_]*' "$SCRIPTS_DIR" 2>/dev/null \
  | sed -E 's/CharacterManager\.Instance\.//' | sort -u)

CM_FILE=$(find "$SCRIPTS_DIR" -type f -name "CharacterManager.cs" | head -1)
DEFINED=""
if [ -n "$CM_FILE" ]; then
  DEFINED=$(grep -oE 'public[[:space:]]+[^[:space:]]+[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*\(' "$CM_FILE" \
    | sed -E 's/public[[:space:]]+[^[:space:]]+[[:space:]]+([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*\(/\1/' | sort -u)
fi

missing=0
while IFS= read -r m; do
  [ -z "$m" ] && continue
  if ! echo "$DEFINED" | grep -qx "$m"; then
    echo "WARNING: CharacterManager.$m() called but not found as public method"
    missing=$((missing+1))
  fi
done <<< "$CALLED"
[ "$missing" -eq 0 ] && echo "All clear - no missing methods detected."

echo '```'
echo
echo "---"
echo "End of audit."
