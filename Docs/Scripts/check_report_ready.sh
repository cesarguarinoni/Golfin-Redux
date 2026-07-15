#!/bin/bash
# Daily-report readiness check. Run any time before 20:30 to confirm tonight will fire.
uid=$(id -u)
echo "── Daily report readiness ($(date '+%a %H:%M')) ──"
li=$(launchctl print gui/$uid/com.golfin.dailyreport 2>/dev/null | grep -oE 'run interval = [0-9]+ seconds')
[ -n "$li" ] && echo "✅ poll loaded ($li)" || echo "❌ job NOT loaded — run: launchctl load ~/Library/LaunchAgents/com.golfin.dailyreport.plist"
pw=$(pmset -g batt | grep -oE "'AC Power'|'Battery Power'")
if [ "$pw" = "'AC Power'" ]; then echo "✅ on AC — Mac never sleeps (sleep 0), poll WILL fire"; else echo "⚠️  on BATTERY — Mac sleeps after 1 min; relies on the 20:29 forced wake. PLUG IN to guarantee."; fi
pmset -g sched | grep -qi "wakepoweron at 8:29PM" && echo "✅ forced wake armed (weekdays 8:29PM)" || echo "⚠️  forced wake NOT set — run: sudo pmset repeat wakeorpoweron MTWRF 20:29:00"
ls=$(cat Docs/Scripts/.last_sent 2>/dev/null | cut -c1-10); td=$(date +%F)
[ "$ls" = "$td" ] && echo "ℹ️  already sent today ($ls)" || echo "✅ not yet sent today — tonight's send is pending"
