#!/bin/bash
INPUT=$(cat)
SESSION_ID=$(echo "$INPUT" | jq -r '.session_id')
TRANSCRIPT=$(echo "$INPUT" | jq -r '.transcript_path')

CONDITION="${EXP_CONDITION:-unknown}"
FEATURE="${EXP_FEATURE:-unspecified}"
LOGFILE="research-logs/${CONDITION}.csv"

mkdir -p research-logs
if [ ! -f "$LOGFILE" ]; then
  echo "timestamp,session_id,feature,turn_count,cum_input_tokens,cum_output_tokens,read,glob,grep,ls,edit,write,bash" > "$LOGFILE"
fi

count_tool () {
  jq -s --arg n "$1" '[.[] | select(.message.content != null) | .message.content[]? | select(.type=="tool_use" and .name==$n)] | length' "$TRANSCRIPT"
}

TURN=$(jq -s '[.[] | select(.type=="assistant")] | length' "$TRANSCRIPT")
IN=$(jq -s '[.[] | .message.usage.input_tokens? // empty] | add // 0' "$TRANSCRIPT")
OUT=$(jq -s '[.[] | .message.usage.output_tokens? // empty] | add // 0' "$TRANSCRIPT")

echo "$(date -Iseconds),$SESSION_ID,$FEATURE,$TURN,$IN,$OUT,$(count_tool Read),$(count_tool Glob),$(count_tool Grep),$(count_tool LS),$(count_tool Edit),$(count_tool Write),$(count_tool Bash)" >> "$LOGFILE"
