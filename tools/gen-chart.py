#!/usr/bin/env python3
"""Generate the GetOrderById chart (HTML -> PNG) straight from a benchmark TSV.

The TSV is the single source of truth: every number that reaches the chart is
read from it here, so the image cannot drift from the measured data.

    python3 tools/gen-chart.py /tmp/bm-ob5.tsv --html /tmp/chart.html \
        --png docs/getorderbyid.png

TSV schema (tab separated):
    op transport conc success failed elapsed_s throughput
    min_ms mean_ms p50_ms p90_ms p95_ms p99_ms max_ms
"""

import argparse
import json
import os
import shutil
import subprocess
import sys

ORDER = ["nettcp", "grpc", "basichttp", "wshttp"]
LABEL = {
    "nettcp": "nettcp",
    "grpc": "grpc",
    "basichttp": "basichttp",
    "wshttp": "wshttp",
}
COLOR = {
    "nettcp": "#10b981",
    "grpc": "#3b82f6",
    "basichttp": "#f59e0b",
    "wshttp": "#f43f5e",
}
CONC = [1, 10, 20, 40, 60, 80, 100]

CHART_JS = "/tmp/chart.umd.min.js"


def read_tsv(path):
    rows = []
    with open(path) as fh:
        header = fh.readline().rstrip("\n").split("\t")
        for line in fh:
            line = line.rstrip("\n")
            if not line.strip():
                continue
            rows.append(dict(zip(header, line.split("\t"))))
    return rows


def collect(rows):
    """Pivot the TSV into {transport: {conc: {...}}} keyed by concurrency."""
    out = {t: {c: {} for c in CONC} for t in ORDER}
    total_calls = 0
    total_failed = 0
    runs = 0
    for r in rows:
        t = r["transport"]
        c = int(r["conc"])
        if t not in out:
            raise SystemExit("unexpected transport %r" % t)
        if c not in out[t]:
            raise SystemExit("unexpected concurrency %r" % c)
        out[t][c] = {
            "tp": float(r["throughput"]),
            "p50": float(r["p50_ms"]),
            "p99": float(r["p99_ms"]),
            "max": float(r["max_ms"]),
            "mean": float(r["mean_ms"]),
        }
        total_calls += int(r["success"])
        total_failed += int(r["failed"])
        runs += 1

    for t in ORDER:
        for c in CONC:
            if not out[t][c]:
                raise SystemExit("missing run: %s @ c=%d" % (t, c))
    return out, total_calls, total_failed, runs


def build_data(series):
    data = {"conc": CONC, "runs": {}, "tp": {}, "p99": {}, "p50": {}, "max": {}, "mean": {}}
    for t in ORDER:
        for key, field in (("tp", "tp"), ("p99", "p99"), ("p50", "p50"), ("max", "max"), ("mean", "mean")):
            data[key][t] = [series[t][c][field] for c in CONC]
    return data


HTML = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>GetOrderById</title>
<link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
<style>
  *{ box-sizing:border-box; }
  html,body{ margin:0; padding:0; background:#fafafa; }
  body{ width:1240px; font-family:'Outfit',system-ui,sans-serif; color:#18181b;
        -webkit-font-smoothing:antialiased; padding:26px; }
  .card{ background:#fff; border:1px solid rgba(0,0,0,.08); border-radius:16px;
         box-shadow:0 2px 8px rgba(0,0,0,.04); padding:24px 26px 18px; }
  .head{ display:flex; align-items:flex-start; justify-content:space-between; gap:24px; }
  .kicker{ font-size:11px; letter-spacing:.09em; text-transform:uppercase; color:#a1a1aa;
           font-weight:600; margin:0 0 6px; }
  h1{ font-size:25px; font-weight:600; letter-spacing:-.015em; margin:0; }
  .sub{ font-size:13px; color:#71717a; margin:7px 0 0; }
  .legend{ display:flex; flex-wrap:wrap; gap:7px; justify-content:flex-end; padding-top:4px; }
  .chip{ display:flex; align-items:center; gap:7px; font-size:12.5px; font-weight:500;
         color:#52525b; background:#fafafa; border:1px solid rgba(0,0,0,.07);
         border-radius:999px; padding:5px 12px 5px 9px; }
  .dot{ width:9px; height:9px; border-radius:50%; flex:none; }
  .grid{ display:grid; grid-template-columns:1fr 1fr; gap:22px; margin-top:20px; }
  .panel h2{ font-size:13.5px; font-weight:600; margin:0 0 2px; }
  .panel p{ font-size:12px; color:#a1a1aa; margin:0 0 10px; }
  .box{ position:relative; width:100%; height:318px; }
  .foot{ font-size:11.5px; color:#a1a1aa; margin-top:16px; padding-top:13px;
         border-top:1px solid rgba(0,0,0,.07); }
  .mono{ font-family:'JetBrains Mono',monospace; }
</style>
</head>
<body>
<div class="card">
  <div class="head">
    <div>
      <p class="kicker">CoreWCF vs gRPC &middot; .NET 10 &middot; Apple M4</p>
      <h1>GetOrderById &mdash; throughput and tail latency</h1>
      <p class="sub">__RUNS__ runs &middot; __CALLS__ calls &middot; __FAILED__ failed &middot; 60&nbsp;s per run</p>
    </div>
    <div class="legend">__LEGEND__</div>
  </div>

  <div class="grid">
    <div class="panel">
      <h2>Throughput</h2>
      <p>requests per second, by concurrency</p>
      <div class="box"><canvas id="tp"></canvas></div>
    </div>
    <div class="panel">
      <h2>Tail latency (p99)</h2>
      <p>milliseconds, log scale</p>
      <div class="box"><canvas id="p99"></canvas></div>
    </div>
  </div>

  <p class="foot">The concurrency axis is linear, so the shape shows where the server saturates: throughput climbs steeply to c=10 and then flattens past c=20. Every point is a measured run, read straight from the benchmark TSV.</p>
</div>

<script src="__CHARTJS__"></script>
<script>
const DATA = __DATA__;
const ORDER = __ORDER__;
const LABEL = __LABEL__;
const COLOR = __COLOR__;
const GRID = 'rgba(0,0,0,.07)';
const nf = v => v.toLocaleString(undefined,{maximumFractionDigits:0});

function datasets(key){
  // A linear x-axis needs explicit {x,y} points; a bare number array is read as
  // index-pairs and plots nothing.
  return ORDER.map(t => ({
    label: LABEL[t],
    data: DATA.conc.map((c,i) => ({ x:c, y: DATA[key][t][i] })),
    borderColor: COLOR[t],
    backgroundColor: COLOR[t],
    borderWidth: 2.5,
    tension: .25,
    pointRadius: 3.6,
    pointHoverRadius: 6,
    pointBorderWidth: 0,
  }));
}

const xScale = {
  type:'linear', grid:{display:false}, border:{color:'rgba(0,0,0,.14)'},
  min: 0, max: 100,
  ticks:{ stepSize: 20, callback:v => v === 0 ? '0' : String(v) },
  title:{ display:true, text:'concurrency (workers)', color:'#a1a1aa', font:{size:11} },
};

function makeLine(id, key, yTitle, yOpts, fmt){
  new Chart(document.getElementById(id), {
    type:'line',
    data:{ datasets: datasets(key) },
    options:{
      responsive:true, maintainAspectRatio:false,
      // Animation must be off: a headless screenshot lands mid-animation and
      // captures the y-axis only partially resolved.
      animation:false,
      interaction:{ mode:'index', intersect:false },
      layout:{ padding:{ top:6 } },
      plugins:{
        legend:{ display:false },
        tooltip:{
          backgroundColor:'#18181b', titleColor:'#fff', bodyColor:'#e4e4e7',
          padding:10, cornerRadius:8, displayColors:true, boxWidth:8, boxHeight:8,
          usePointStyle:true,
          callbacks:{
            title: items => 'concurrency ' + items[0].parsed.x,
            label: c => '  ' + c.dataset.label + ': ' + fmt(c.parsed.y),
          }
        }
      },
      scales:{
        x: xScale,
        y: { grid:{color:GRID}, border:{display:false},
             title:{ display:true, text:yTitle, color:'#a1a1aa', font:{size:11} },
             ...yOpts }
      }
    }
  });
}

makeLine('tp','tp','req/s', { beginAtZero:true }, v => nf(v) + ' req/s');
makeLine('p99','p99','p99 latency (ms)',
  { type:'logarithmic', beginAtZero:false,
    afterBuildTicks:(ax)=>{ ax.ticks=[0.1,0.2,0.5,1,2,5,10].map(v=>({value:v})); },
    ticks:{ callback:v => String(v) + ' ms' } },
  v => v.toFixed(3) + ' ms');
</script>
</body>
</html>
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("tsv")
    ap.add_argument("--html", default="/tmp/chart.html")
    ap.add_argument("--png", default="docs/getorderbyid.png")
    ap.add_argument("--scale", type=float, default=2.0)
    args = ap.parse_args()

    rows = read_tsv(args.tsv)
    series, calls, failed, runs = collect(rows)
    data = build_data(series)

    legend = "".join(
        '<span class="chip"><span class="dot" style="background:%s"></span>%s</span>' % (COLOR[t], LABEL[t])
        for t in ORDER
    )

    html = (
        HTML.replace("__RUNS__", "{:,}".format(runs))
        .replace("__CALLS__", "{:,}".format(calls))
        .replace("__FAILED__", "{:,}".format(failed))
        .replace("__LEGEND__", legend)
        .replace("__CHARTJS__", CHART_JS)
        .replace("__DATA__", json.dumps(data))
        .replace("__ORDER__", json.dumps(ORDER))
        .replace("__LABEL__", json.dumps(LABEL))
        .replace("__COLOR__", json.dumps(COLOR))
    )

    with open(args.html, "w") as fh:
        fh.write(html)

    chrome = shutil.which("google-chrome") or "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
    out = os.path.abspath(args.png)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    if os.path.exists(out):
        os.remove(out)

    cmd = [
        chrome,
        "--headless=new",
        "--disable-gpu",
        "--hide-scrollbars",
        "--force-device-scale-factor=%g" % args.scale,
        "--window-size=1240,700",
        "--default-background-color=00000000",
        "--screenshot=%s" % out,
        "file://" + os.path.abspath(args.html),
    ]
    res = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
    if not os.path.exists(out):
        sys.stderr.write(res.stdout + res.stderr)
        raise SystemExit("chrome did not produce %s" % out)

    print("runs=%d calls=%d failed=%d" % (runs, calls, failed))
    print("html=%s" % args.html)
    print("png=%s (%d bytes)" % (out, os.path.getsize(out)))


if __name__ == "__main__":
    main()
