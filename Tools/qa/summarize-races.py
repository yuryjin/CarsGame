#!/usr/bin/env python3
"""Summarize RaceHarness measurements; Unity exit 0 alone is not a completed race."""
import argparse
import csv
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
args = parser.parse_args()
rows = []
for path in sorted(args.directory.glob('race_*.json')):
    report = json.loads(path.read_text())
    players = [c for c in report['cars'] if c['player']]
    if len(players) != 1:
        raise ValueError(f'{path}: expected one player')
    car = players[0]
    rows.append(dict(
        case=path.stem.removeprefix('race_'), event=report['evt'], tier=report['tier'], seed=report['seed'], body=car['body'],
        reversed=report.get('reversed'), turrets_enabled=report.get('turretsEnabled'),
        player_finished=car['finished'], simulation_limit=report['ticks'] > 50 * 600,
        field_finished=sum(c['finished'] for c in report['cars']), field_size=len(report['cars']),
        player_seconds=round(car['finishSeconds'], 2), race_seconds=round(report['raceSeconds'], 2), laps=car['laps'],
        deaths=car['deaths'], offroad_seconds=round(car['offTrackSeconds'], 2), wall_impacts=car['wallImpacts'],
        automatic_recoveries=car['autoResets'], ai_recoveries=car['aiResets'],
        total_resets=car.get('resets'), shortcuts=car.get('shortcuts'),
        field_ai_recoveries=sum(c['aiResets'] for c in report['cars']),
    ))
if not rows:
    raise SystemExit('No race reports yet')
with (args.directory / 'summary.csv').open('w', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
summary = dict(cases=len(rows), player_finishes=sum(r['player_finished'] for r in rows),
               player_dnf=sum(not r['player_finished'] for r in rows),
               simulation_limits=[r['case'] for r in rows if r['simulation_limit']],
               highest_recoveries=sorted(rows, key=lambda r: r['automatic_recoveries'] + r['ai_recoveries'], reverse=True)[:5],
               highest_total_resets=sorted((r for r in rows if r['total_resets'] is not None), key=lambda r: r['total_resets'], reverse=True)[:5])
(args.directory / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
print(f"{summary['cases']} measurements: {summary['player_finishes']} player finishes, {summary['player_dnf']} DNF, {len(summary['simulation_limits'])} simulation limits")
for row in summary['highest_total_resets'] or summary['highest_recoveries']:
    print(row['case'], 'total resets', row['total_resets'] if row['total_resets'] is not None else 'unmeasured',
          'auto', row['automatic_recoveries'], 'AI', row['ai_recoveries'], 'deaths', row['deaths'])
