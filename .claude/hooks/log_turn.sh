#!/bin/bash
JQ="/c/Users/dleks/AppData/Local/Microsoft/WinGet/Packages/jqlang.jq_Microsoft.Winget.Source_8wekyb3d8bbwe/jq.exe"

INPUT=$(cat)
SESSION_ID=$(echo "$INPUT" | "$JQ" -r '.session_id')
TRANSCRIPT=$(echo "$INPUT" | "$JQ" -r '.transcript_path')

CONDITION="${EXP_CONDITION:-unknown}"
FEATURE="${EXP_FEATURE:-unspecified}"
LOGFILE="research-logs/${CONDITION}.csv"

mkdir -p research-logs
if [ ! -f "$LOGFILE" ]; then
  echo "timestamp,session_id,feature,turn_count,cum_input_tokens,cum_output_tokens,cum_cache_creation_tokens,cum_cache_read_tokens,read,glob,grep,ls,edit,write,bash" > "$LOGFILE"
fi

count_tool () {
  "$JQ" -s --arg n "$1" '[.[] | select(.message.content != null) | .message.content[]? | select(.type=="tool_use" and .name==$n)] | length' "$TRANSCRIPT"
}

TURN=$("$JQ" -s '[.[] | select(.type=="assistant")] | length' "$TRANSCRIPT")
IN=$("$JQ" -s '[.[] | .message.usage.input_tokens? // empty] | add // 0' "$TRANSCRIPT")
OUT=$("$JQ" -s '[.[] | .message.usage.output_tokens? // empty] | add // 0' "$TRANSCRIPT")
CACHE_CREATE=$("$JQ" -s '[.[] | .message.usage.cache_creation_input_tokens? // empty] | add // 0' "$TRANSCRIPT")
CACHE_READ=$("$JQ" -s '[.[] | .message.usage.cache_read_input_tokens? // empty] | add // 0' "$TRANSCRIPT")

echo "$(date -Iseconds),$SESSION_ID,$FEATURE,$TURN,$IN,$OUT,$CACHE_CREATE,$CACHE_READ,$(count_tool Read),$(count_tool Glob),$(count_tool Grep),$(count_tool LS),$(count_tool Edit),$(count_tool Write),$(count_tool Bash)" >> "$LOGFILE"
