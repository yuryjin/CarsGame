#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

function main() {
  const [inPath, outPath] = process.argv.slice(2);
  if (!inPath || !outPath) throw new Error('usage: ua-tour-analyze.js <input.json> <output.json>');
  const input = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const nodes = input.nodes || [];
  const edges = input.edges || [];
  const layers = input.layers || [];
  const ids = new Set(nodes.map(n => n.id));
  const byId = new Map(nodes.map(n => [n.id, n]));

  // Only consider edges whose both ends are file-level nodes present in input
  const fileEdges = edges.filter(e => ids.has(e.source) && ids.has(e.target) && e.source !== e.target);

  // A/B fan-in / fan-out (distinct neighbours)
  const fanInSets = new Map(), fanOutSets = new Map();
  for (const n of nodes) { fanInSets.set(n.id, new Set()); fanOutSets.set(n.id, new Set()); }
  for (const e of fileEdges) { fanOutSets.get(e.source).add(e.target); fanInSets.get(e.target).add(e.source); }
  const fanIn = id => fanInSets.get(id).size, fanOut = id => fanOutSets.get(id).size;
  const fanInRanking = nodes.map(n => ({ id: n.id, fanIn: fanIn(n.id), name: n.name })).sort((a, b) => b.fanIn - a.fanIn).slice(0, 20);
  const fanOutRanking = nodes.map(n => ({ id: n.id, fanOut: fanOut(n.id), name: n.name })).sort((a, b) => b.fanOut - a.fanOut).slice(0, 20);

  // C entry point candidates
  const entryNames = new Set(['index.ts','index.js','main.ts','main.js','app.ts','app.js','server.ts','server.js','mod.rs','main.go','main.py','main.rs','manage.py','app.py','wsgi.py','asgi.py','run.py','__main__.py','Application.java','Main.java','Program.cs','config.ru','index.php','App.swift','Application.kt','main.cpp','main.c']);
  const codeNodes = nodes.filter(n => n.type === 'file');
  const fanOutVals = codeNodes.map(n => fanOut(n.id)).sort((a, b) => b - a);
  const fanInVals = codeNodes.map(n => fanIn(n.id)).sort((a, b) => a - b);
  const top10Cut = fanOutVals[Math.max(0, Math.ceil(fanOutVals.length * 0.1) - 1)] ?? 0;
  const bottom25Cut = fanInVals[Math.max(0, Math.ceil(fanInVals.length * 0.25) - 1)] ?? 0;
  const depth = fp => (fp || '').split('/').filter(Boolean).length - 1;
  const entryPointCandidates = nodes.map(n => {
    let score = 0;
    const fp = n.filePath || n.name || '';
    if (n.type === 'file') {
      if (entryNames.has(n.name)) score += 3;
      if (depth(fp) <= 1) score += 1;
      if (fanOut(n.id) >= top10Cut && fanOut(n.id) > 0) score += 1;
      if (fanIn(n.id) <= bottom25Cut) score += 1;
    } else if (n.type === 'document') {
      if (n.name === 'README.md' && depth(fp) === 0) score += 5;
      else if (/\.md$/i.test(n.name) && depth(fp) === 0) score += 2;
      else if (/\.md$/i.test(n.name)) score += 1;
    }
    return { id: n.id, score, name: n.name, type: n.type, summary: n.summary };
  }).sort((a, b) => b.score - a.score).slice(0, 5);

  // D BFS from top code entry point following imports/calls
  const topCode = entryPointCandidates.find(c => c.type === 'file') || codeNodes.map(n => ({ id: n.id })).sort((a, b) => fanOut(b.id) - fanOut(a.id))[0];
  const adj = new Map(); for (const n of nodes) adj.set(n.id, []);
  for (const e of fileEdges) if (e.type === 'imports' || e.type === 'calls') adj.get(e.source).push(e.target);
  const order = [], depthMap = {};
  if (topCode) {
    const q = [topCode.id]; depthMap[topCode.id] = 0;
    while (q.length) { const cur = q.shift(); order.push(cur); for (const nx of adj.get(cur)) if (!(nx in depthMap)) { depthMap[nx] = depthMap[cur] + 1; q.push(nx); } }
  }
  const byDepth = {}; for (const [id, d] of Object.entries(depthMap)) (byDepth[d] = byDepth[d] || []).push(id);

  // E non-code inventory
  const pick = n => ({ id: n.id, name: n.name, type: n.type, summary: n.summary });
  const nonCodeFiles = {
    documentation: nodes.filter(n => n.type === 'document').map(pick),
    infrastructure: nodes.filter(n => ['service','pipeline','resource'].includes(n.type)).map(pick),
    data: nodes.filter(n => ['table','schema','endpoint'].includes(n.type)).map(pick),
    config: nodes.filter(n => n.type === 'config').map(pick),
  };

  // F clusters: bidirectional pairs then expand
  const key = (a, b) => a < b ? a + '|' + b : b + '|' + a;
  const pairTypes = new Map();
  for (const e of fileEdges) if (e.type === 'imports' || e.type === 'calls') { const k = e.source + '>' + e.target + ':' + e.type; pairTypes.set(k, true); }
  const seeds = [];
  for (const e of fileEdges) if ((e.type === 'imports' || e.type === 'calls') && pairTypes.has(e.target + '>' + e.source + ':' + e.type)) seeds.push([e.source, e.target]);
  const undirected = new Map(); for (const n of nodes) undirected.set(n.id, new Set());
  for (const e of fileEdges) { undirected.get(e.source).add(e.target); undirected.get(e.target).add(e.source); }
  const clusters = [];
  const seen = new Set();
  for (const [a, b] of seeds) {
    const k = key(a, b); if (seen.has(k)) continue;
    const members = new Set([a, b]);
    let grew = true;
    while (grew && members.size < 5) {
      grew = false;
      for (const n of nodes) { if (members.has(n.id)) continue; let c = 0; for (const m of members) if (undirected.get(n.id).has(m)) c++; if (c >= 2) { members.add(n.id); grew = true; if (members.size >= 5) break; } }
    }
    for (const m of members) for (const m2 of members) if (m < m2) seen.add(key(m, m2));
    const edgeCount = fileEdges.filter(e => members.has(e.source) && members.has(e.target)).length;
    clusters.push({ nodes: [...members], edgeCount });
  }
  clusters.sort((x, y) => y.edgeCount - x.edgeCount);

  // H index
  const nodeSummaryIndex = {}; for (const n of nodes) nodeSummaryIndex[n.id] = { name: n.name, type: n.type, summary: n.summary };

  const out = {
    scriptCompleted: true, entryPointCandidates, fanInRanking, fanOutRanking,
    bfsTraversal: { startNode: topCode ? topCode.id : null, order, depthMap, byDepth },
    nonCodeFiles, clusters: clusters.slice(0, 10), layers: { count: layers.length, list: layers },
    nodeSummaryIndex, totalNodes: nodes.length, totalEdges: edges.length, fileLevelEdges: fileEdges.length,
  };
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));
  console.log('ok: ' + out.totalNodes + ' nodes, ' + out.totalEdges + ' edges, bfs start ' + out.bfsTraversal.startNode);
}
try { main(); } catch (e) { console.error(e && e.stack || e); process.exit(1); }
