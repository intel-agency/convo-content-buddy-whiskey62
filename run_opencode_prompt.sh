#!/usr/bin/env bash
set -euo pipefail

usage() {
    echo "Usage: $0 -f <file> | -p <prompt> [-a <url>] [-u <user>] [-P <pass>] [-d <dir>] [-l <log-level>] [-L]" >&2
    echo "  -f <file>       Read prompt from file" >&2
    echo "  -p <prompt>     Use prompt string directly" >&2
    echo "  -a <url>        Attach to a running opencode server (e.g. https://host:4096)" >&2
    echo "  -u <user>       Basic auth username (prefer env var OPENCODE_AUTH_USER)" >&2
    echo "  -P <pass>       Basic auth password (prefer env var OPENCODE_AUTH_PASS)" >&2
    echo "  -d <dir>        Working directory on the server (used with -a)" >&2
    echo "  -l <log-level>  opencode log level (DEBUG|INFO|WARN|ERROR), default: INFO" >&2
    echo "  -L              Enable --print-logs (disabled by default)" >&2
    echo "" >&2
    echo "  Credentials are resolved in order: flags > env vars OPENCODE_AUTH_USER / OPENCODE_AUTH_PASS" >&2
    exit 1
}

prompt=""
attach_url=""
auth_user="${OPENCODE_AUTH_USER:-}"   # prefer env vars — flags override if provided
auth_pass="${OPENCODE_AUTH_PASS:-}"
work_dir=""
log_level="INFO"
print_logs=""

while getopts ":f:p:a:u:P:d:l:L" opt; do
    case $opt in
        f) prompt=$(cat "$OPTARG") ;;
        p) prompt="$OPTARG" ;;
        a) attach_url="$OPTARG" ;;
        u) auth_user="$OPTARG" ;;
        P) auth_pass="$OPTARG" ;;
        d) work_dir="$OPTARG" ;;
        l) log_level="$OPTARG" ;;
        L) print_logs="--print-logs" ;;
        *) usage ;;
    esac
done

if [ -z "$prompt" ]; then
    usage
fi

if [[ -z "${ZHIPU_API_KEY:-}" ]]; then
    echo "::error::ZHIPU_API_KEY is not set" >&2
    exit 1
fi

if [[ -z "${KIMI_CODE_ORCHESTRATOR_AGENT_API_KEY:-}" ]]; then
    echo "::error::KIMI_CODE_ORCHESTRATOR_AGENT_API_KEY is not set" >&2
    exit 1
fi

# Authenticate GitHub CLI and set MCP-compatible token
if [[ -n "${GITHUB_TOKEN:-}" ]]; then
    export GITHUB_PERSONAL_ACCESS_TOKEN="${GITHUB_TOKEN}"
    export GH_TOKEN="${GITHUB_TOKEN}"
    if echo "${GITHUB_TOKEN}" | gh auth login --with-token 2>&1; then
        echo "gh CLI authenticated successfully"
        gh auth status
    else
        echo "::warning::gh auth login failed — gh CLI commands may not work"
    fi
else
    echo "::warning::GITHUB_TOKEN is not set — gh CLI will not be authenticated"
fi

# Embed basic auth credentials into the attach URL if provided
if [[ -n "$attach_url" && -n "$auth_user" && -n "$auth_pass" ]]; then
    # Warn if credentials are being sent over plain HTTP
    if [[ "$attach_url" == http://* ]]; then
        echo "::warning::Basic auth credentials over http:// are sent in plaintext — use https://" >&2
    fi
    scheme="${attach_url%%://*}"
    rest="${attach_url#*://}"
    attach_url="${scheme}://${auth_user}:${auth_pass}@${rest}"
elif [[ ( -n "$auth_user" || -n "$auth_pass" ) && -z "$attach_url" ]]; then
    echo "::error::OPENCODE_AUTH_USER/PASS (or -u/-P) require -a <url>" >&2
    exit 1
fi

# Build opencode args — optional flags only included when set
opencode_args=(
    run
    --model zai-coding-plan/glm-5
    --agent orchestrator
    --log-level "$log_level"
)
[[ -n "$attach_url" ]] && opencode_args+=(--attach "$attach_url")
[[ -n "$work_dir"   ]] && opencode_args+=(--dir    "$work_dir")
[[ -n "$print_logs" ]] && opencode_args+=("$print_logs")
opencode_args+=("$prompt")

echo "Starting opencode at $(date -u +%Y-%m-%dT%H:%M:%SZ)"

# Idle watchdog: kill opencode if it produces no output for IDLE_TIMEOUT_SECS.
# An active agent continuously emits tool calls, reasoning, etc. Sustained silence
# means it's stuck. This replaces a hard wall-clock timeout so long-running but
# actively-working agents aren't killed prematurely.
IDLE_TIMEOUT_SECS=900   # 15 minutes of no output
HARD_CEILING_SECS=5400  # 90-minute absolute safety net
OUTPUT_LOG=$(mktemp /tmp/opencode-output.XXXXXX)

set +e

# Start opencode with output redirected to a log file
stdbuf -oL -eL opencode "${opencode_args[@]}" > "$OUTPUT_LOG" 2>&1 &
OPENCODE_PID=$!

# Stream the log to stdout in real-time so CI can see it
tail -f "$OUTPUT_LOG" &
TAIL_PID=$!

START_TIME=$(date +%s)
IDLE_KILLED=0

# Watchdog loop: check output freshness every 30 seconds
while kill -0 "$OPENCODE_PID" 2>/dev/null; do
    sleep 30

    # Hard ceiling safety net
    now=$(date +%s)
    elapsed=$(( now - START_TIME ))
    if [[ $elapsed -ge $HARD_CEILING_SECS ]]; then
        echo ""
        echo "::warning::opencode hit ${HARD_CEILING_SECS}s hard ceiling; terminating"
        kill "$OPENCODE_PID" 2>/dev/null
        IDLE_KILLED=1
        break
    fi

    # Idle detection: check last modification time of the output log
    last_mod=$(stat -c %Y "$OUTPUT_LOG" 2>/dev/null || echo "$now")
    idle=$(( now - last_mod ))
    if [[ $idle -ge $IDLE_TIMEOUT_SECS ]]; then
        echo ""
        echo "::warning::opencode idle for $(( idle / 60 ))m (no output); terminating"
        kill "$OPENCODE_PID" 2>/dev/null
        IDLE_KILLED=1
        break
    fi
done

wait "$OPENCODE_PID" 2>/dev/null
OPENCODE_EXIT=$?
kill "$TAIL_PID" 2>/dev/null
wait "$TAIL_PID" 2>/dev/null
rm -f "$OUTPUT_LOG"

set -e

if [[ $IDLE_KILLED -eq 1 ]]; then
    exit 0
fi

exit ${OPENCODE_EXIT}
