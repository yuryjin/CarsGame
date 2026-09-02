#!/usr/bin/env node
const fs = require('fs');
const path = require('path');
const [,, inPath, outPath] = process.argv;
if (!inPath || !outPath) { console.error('usage: ua-arch-analyze.js <input.json> <output.json>'); process.exit(1); }
let input;
try { input = JSON.parse(fs.readFileSync(inPath, 'utf8')); } catch (e) { console.error('cannot read input: ' + e.message); process.exit(1); }
const fileNodes = input.fileNodes || [];
const importEdges = input.importEdges || [];
const allEdges = input.allEdges || [];
const byId = new Map(fileNodes.map(n => [n.id, n]));
const fp = n => (n.filePath || '').replace(/\\/g, '/');

// A. Directory grouping
const segsList = fileNodes.map(n => fp(n).split('/'));
let prefix = [];
if (segsList.length) {
  const first = segsList[0].slice(0, -1);
  for (let i = 0; i < first.length; i++) {
    if (segsList.every(s => s.length - 1 > i && s[i] === first[i])) prefix.push(first[i]); else break;
  }
}
const groupOf = n => {
  const segs = fp(n).split('/').slice(prefix.length);
  return segs.length > 1 ? segs[0] : 'root';
};
const directoryGroups = {};
for (const n of fileNodes) (directoryGroups[groupOf(n)] ||= []).push(n.id);
// flat-structure fallback: group by extension pattern
const flat = Object.keys(directoryGroups).length === 1 && Object.keys(directoryGroups)[0] === 'root';
if (flat) {
  for (const k in directoryGroups) delete directoryGroups[k];
  for (const n of fileNodes) {
    const name = n.name || '';
    const g = /\.(test|spec)\./.test(name) ? 'test' : /config/i.test(name) ? 'config' : (path.extname(name).slice(1) || 'other');
    (directoryGroups[g] ||= []).push(n.id);
  }
}
const groupOfId = {};
for (const [g, ids] of Object.entries(directoryGroups)) ids.forEach(id => groupOfId[id] = g);

// B. Node type grouping
const nodeTypeGroups = {};
for (const n of fileNodes) (nodeTypeGroups[n.type] ||= []).push(n.id);

// C. Import adjacency
const fileFanIn = {}, fileFanOut = {};
const groupImportsFrom = {}, groupImportedBy = {};
for (const e of importEdges) {
  fileFanOut[e.source] = (fileFanOut[e.source] || 0) + 1;
  fileFanIn[e.target] = (fileFanIn[e.target] || 0) + 1;
  const gs = groupOfId[e.source], gt = groupOfId[e.target];
  if (gs && gt && gs !== gt) {
    (groupImportsFrom[gs] ||= new Set()).add(gt);
    (groupImportedBy[gt] ||= new Set()).add(gs);
  }
}
const setObj = o => Object.fromEntries(Object.entries(o).map(([k, v]) => [k, [...v]]));

// D. Cross-category edges
const cross = {};
for (const e of allEdges) {
  const s = byId.get(e.source), t = byId.get(e.target);
  if (!s || !t) continue;
  const key = `${s.type}|${t.type}|${e.type}`;
  cross[key] = (cross[key] || 0) + 1;
}
const crossCategoryEdges = Object.entries(cross).map(([k, count]) => { const [fromType, toType, edgeType] = k.split('|'); return { fromType, toType, edgeType, count }; });
const nonCodeToCode = allEdges.filter(e => { const s = byId.get(e.source), t = byId.get(e.target); return s && t && s.type !== 'file' && t.type === 'file'; })
  .map(e => ({ source: e.source, target: e.target, type: e.type }));

// E. Inter-group import frequency
const inter = {};
for (const e of importEdges) {
  const gs = groupOfId[e.source], gt = groupOfId[e.target];
  if (gs && gt && gs !== gt) inter[`${gs}|${gt}`] = (inter[`${gs}|${gt}`] || 0) + 1;
}
const interGroupImports = Object.entries(inter).map(([k, count]) => { const [from, to] = k.split('|'); return { from, to, count }; });

// F. Intra-group density
const intraGroupDensity = {};
for (const g of Object.keys(directoryGroups)) {
  let internal = 0, total = 0;
  for (const e of importEdges) {
    const gs = groupOfId[e.source], gt = groupOfId[e.target];
    if (gs === g || gt === g) { total++; if (gs === g && gt === g) internal++; }
  }
  intraGroupDensity[g] = { internalEdges: internal, totalEdges: total, density: total ? +(internal / total).toFixed(2) : 0 };
}

// G. Pattern matching
const dirPatterns = [
  [['routes','api','controllers','endpoints','handlers','controller','routers','serializers','blueprints'], 'api'],
  [['services','core','lib','domain','logic','signals','internal','composables','mailers','jobs','channels'], 'service'],
  [['models','db','data','persistence','repository','entities','migrations','entity','sql','database','schema'], 'data'],
  [['components','views','pages','ui','layouts','screens'], 'ui'],
  [['middleware','plugins','interceptors','guards'], 'middleware'],
  [['utils','helpers','common','shared','tools','templatetags','pkg'], 'utility'],
  [['config','constants','env','settings','management','commands','projectsettings'], 'config'],
  [['__tests__','test','tests','spec','specs'], 'test'],
  [['types','interfaces','schemas','contracts','dtos','dto','request','response'], 'types'],
  [['hooks'], 'hooks'], [['store','state','reducers','actions','slices'], 'state'],
  [['assets','static','public'], 'assets'], [['cmd','bin'], 'entry'],
  [['docs','documentation','wiki'], 'documentation'],
  [['deploy','deployment','infra','infrastructure','k8s','kubernetes','helm','charts','terraform','tf','docker'], 'infrastructure'],
  [['.github','.gitlab','.circleci'], 'ci-cd'],
  [['scripts'], 'service'], [['packages'], 'config'],
];
const patternMatches = {};
for (const g of Object.keys(directoryGroups)) {
  const lg = g.toLowerCase();
  const hit = dirPatterns.find(([names]) => names.includes(lg));
  patternMatches[g] = hit ? hit[1] : 'unknown';
}
const filePattern = n => {
  const name = n.name || '', p = fp(n);
  if (/(\.test\.|\.spec\.|^test_.*\.py$|_test\.go$|Test\.java$|_spec\.rb$|Test\.php$|Tests\.cs$)/.test(name)) return 'test';
  if (/\.d\.ts$/.test(name)) return 'types';
  if (/^(index\.(ts|js)|__init__\.py|manage\.py|config\.ru)$/.test(name) || /^(main\.go|main\.rs|lib\.rs|Application\.java|Program\.cs)$/.test(name)) return 'entry';
  if (/^(wsgi|asgi)\.py$/.test(name)) return 'config';
  if (/^(Cargo\.toml|go\.mod|Gemfile|pom\.xml|build\.gradle|composer\.json|manifest\.json|package\.json)$/.test(name)) return 'config';
  if (/^Dockerfile|^docker-compose|\.tf(vars)?$|^Makefile$/.test(name)) return 'infrastructure';
  if (/\.github\/workflows\//.test(p) || /^(\.gitlab-ci\.yml|Jenkinsfile)$/.test(name)) return 'ci-cd';
  if (/\.sql$/.test(name)) return 'data';
  if (/\.(graphql|gql|proto)$/.test(name)) return 'types';
  if (/\.(md|rst)$/.test(name)) return 'documentation';
  if (/\.(inputactions|asset)$/.test(name) || /^ProjectSettings\//.test(p) || /Settings\.json$/.test(name)) return 'config';
  return null;
};
const filePatternMatches = {};
for (const n of fileNodes) { const m = filePattern(n); if (m) filePatternMatches[n.id] = m; }

// H. Deployment topology
const names = fileNodes.map(n => fp(n));
const deploymentTopology = {
  hasDockerfile: names.some(p => /Dockerfile/.test(p)),
  hasCompose: names.some(p => /docker-compose/.test(p)),
  hasK8s: names.some(p => /(k8s|kubernetes|helm)\//.test(p)),
  hasTerraform: names.some(p => /\.tf$/.test(p)),
  hasCI: names.some(p => /\.github\/workflows|\.gitlab-ci|Jenkinsfile/.test(p)),
  infraFiles: names.filter(p => /Dockerfile|docker-compose|\.tf$|\.github\/workflows|\.gitlab-ci|Jenkinsfile|Makefile/.test(p)),
};

// I. Data pipeline
const dataPipeline = {
  schemaFiles: names.filter(p => /\.(sql|graphql|gql|proto|prisma)$/.test(p) && !/migrations\//.test(p)),
  migrationFiles: names.filter(p => /migrations\//.test(p)),
  dataModelFiles: fileNodes.filter(n => (n.tags || []).some(t => /model|entity|schema/i.test(t))).map(fp),
  apiHandlerFiles: fileNodes.filter(n => (n.tags || []).some(t => /api-handler|endpoint|controller/i.test(t))).map(fp),
};

// J. Documentation coverage
const docNodes = fileNodes.filter(n => n.type === 'document');
const groupsWithDocs = new Set();
for (const d of docNodes) {
  groupsWithDocs.add(groupOfId[d.id]);
  for (const e of allEdges) if (e.source === d.id && e.type === 'documents' && groupOfId[e.target]) groupsWithDocs.add(groupOfId[e.target]);
}
const totalGroups = Object.keys(directoryGroups).length;
const docCoverage = {
  groupsWithDocs: groupsWithDocs.size, totalGroups,
  coverageRatio: totalGroups ? +(groupsWithDocs.size / totalGroups).toFixed(2) : 0,
  undocumentedGroups: Object.keys(directoryGroups).filter(g => !groupsWithDocs.has(g)),
};

// K. Dependency direction (imports + configures/depends_on/documents at group level)
const dir = {};
for (const e of allEdges) {
  if (!['imports', 'depends_on', 'configures', 'documents'].includes(e.type)) continue;
  const gs = groupOfId[e.source], gt = groupOfId[e.target];
  if (gs && gt && gs !== gt) dir[`${gs}|${gt}`] = (dir[`${gs}|${gt}`] || 0) + 1;
}
const dependencyDirection = [];
const seen = new Set();
for (const k of Object.keys(dir)) {
  const [a, b] = k.split('|');
  const key = [a, b].sort().join('|');
  if (seen.has(key)) continue; seen.add(key);
  const ab = dir[`${a}|${b}`] || 0, ba = dir[`${b}|${a}`] || 0;
  if (ab >= ba) dependencyDirection.push({ dependent: a, dependsOn: b, edges: ab, reverse: ba });
  else dependencyDirection.push({ dependent: b, dependsOn: a, edges: ba, reverse: ab });
}

const filesPerGroup = Object.fromEntries(Object.entries(directoryGroups).map(([g, ids]) => [g, ids.length]));
const nodeTypeCounts = Object.fromEntries(Object.entries(nodeTypeGroups).map(([t, ids]) => [t, ids.length]));
const out = {
  scriptCompleted: true, commonPrefix: prefix.join('/'), flatStructure: flat,
  directoryGroups, nodeTypeGroups, crossCategoryEdges, nonCodeToCode,
  groupImportsFrom: setObj(groupImportsFrom), groupImportedBy: setObj(groupImportedBy),
  interGroupImports, intraGroupDensity, patternMatches, filePatternMatches,
  deploymentTopology, dataPipeline, docCoverage, dependencyDirection,
  fileStats: { totalFileNodes: fileNodes.length, filesPerGroup, nodeTypeCounts },
  fileFanIn, fileFanOut,
};
fs.writeFileSync(outPath, JSON.stringify(out, null, 2));
process.exit(0);
