// Logic-only web-shell regression: real template + adapter, no browser or network.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';
const templateRoot = process.env.TEMPLATE_ROOT || new URL('../../Assets/WebGLTemplates/Yandex/', import.meta.url);
const root = String(templateRoot).startsWith('file:') ? new URL(templateRoot) : new URL(`file://${templateRoot.replace(/\/$/, '')}/`);
const adapter = await fs.readFile(new URL('TemplateData/cars-platform.js', root), 'utf8');
const html = await fs.readFile(new URL('index.html', root), 'utf8');
const inline = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].at(-1)[1]
  .replace(/^#.*$/gm, '').replace(/\{\{\{ JSON\.stringify\([^}]+\) \}\}\}/g, '"test"').replace(/\{\{\{[^}]+\}\}\}/g, 'artifact');

async function fixture({ language = 'en', sdkLanguage = language, failure = 'unity' } = {}) {
  const nodes = new Map(), timers = new Map(); let timerId = 0, reloads = 0, attached = 0, starts = 0, focused;
  function element(tag) {
    const node = { tagName: tag, style: {}, children: [], attributes: {}, textContent: '',
      appendChild(child) { this.children.push(child); if (child.id) nodes.set(child.id, child); return child; },
      setAttribute(key, value) { this.attributes[key] = value; }, focus() { focused = this; } };
    // The retained before-version builds its retry button with innerHTML.
    Object.defineProperty(node, 'innerHTML', { set(value) { const match = value.match(/<button id="([^"]+)">([^<]+)<\/button>/); if (match) { const button = element('button'); button.id = match[1]; button.textContent = match[2]; node.appendChild(button); } } });
    return node;
  }
  const body = element('body');
  for (const id of ['cars-root', 'cars-loading', 'cars-loading-fill', 'unity-canvas']) { const n = element('div'); n.id = id; body.appendChild(n); }
  const context = { console: { log() {}, warn() {}, error() {} }, TextEncoder, performance,
    location: { hostname: 'games.example', protocol: 'https:', search: '', reload() { reloads++; } },
    navigator: { userAgent: 'logic test', language },
    document: { body, head: element('head'), documentElement: {}, hidden: false,
      createElement: element, getElementById: id => nodes.get(id), querySelector: selector => nodes.get(selector.slice(1)), addEventListener() {} },
    addEventListener() {}, setTimeout(fn, ms) { timers.set(++timerId, { fn, ms }); return timerId; }, clearTimeout(id) { timers.delete(id); } };
  context.window = context;
  const sdk = { environment: { i18n: { lang: sdkLanguage } }, deviceInfo: {}, on() {},
    getPlayer: () => Promise.resolve({ getUniqueID: () => 'guest', getName: () => 'Guest' }) };
  if (failure !== 'sdk-missing') context.YaGames = { init: () => failure === 'sdk-reject' ? Promise.reject(new Error('offline')) : failure === 'sdk-timeout' ? new Promise(() => {}) : Promise.resolve(sdk) };
  if (failure !== 'loader') context.createUnityInstance = (_canvas, config) => {
    starts++; context.unityConfig = config;
    if (failure === 'banner') { config.showBanner('<private diagnostics>', 'error'); return new Promise(() => {}); }
    if (failure === 'warning') config.showBanner('Nonfatal warning', 'warning');
    return failure === 'unity' ? Promise.reject(new Error('<private diagnostics>')) : Promise.resolve({ SendMessage() { attached++; } });
  };
  vm.createContext(context);
  if (failure !== 'adapter') vm.runInContext(adapter, context);
  let startupError;
  try { vm.runInContext(inline, context); } catch (error) { startupError = String(error); }
  // Flush the actual SDK -> player -> Unity promise chain.
  for (let i = 0; i < 30; i++) await Promise.resolve();
  if (failure === 'sdk-timeout') { for (const timer of [...timers.values()]) if (timer.ms === 20000) timer.fn(); for (let i = 0; i < 10; i++) await Promise.resolve(); }
  return { context, nodes, timers, startupError, get starts() { return starts; }, get reloads() { return reloads; }, get focused() { return focused; } };
}

const cases = [];
async function test(name, run) { try { await run(); cases.push({ name, passed: true }); console.log('PASS', name); }
  catch (error) { cases.push({ name, passed: false, error: String(error) }); console.error('FAIL', name, String(error)); } }
for (const failure of ['unity', 'loader', 'adapter', 'banner', 'sdk-missing', 'sdk-reject', 'sdk-timeout']) await test(`recover ${failure}`, async () => {
  const f = await fixture({ failure }); const retry = f.nodes.get('cars-retry'), button = f.nodes.get('cars-retry-btn');
  assert(retry, 'A failed boot must show a retry instead of an endless loading bar');
  assert.equal(f.startupError, undefined, 'Bootstrap failures are handled inside the startup chain');
  assert.equal(retry.style.display, 'flex'); assert.equal(f.nodes.get('cars-loading').style.display, 'none');
  assert.equal(f.focused, button); assert.equal(button.textContent, 'Try again');
  assert(!JSON.stringify(retry).includes('<private diagnostics>'));
  button.onclick(); assert.equal(f.reloads, 1); assert(!f.context.unityInstance);
  if (failure.startsWith('sdk')) assert.equal(f.starts, 0);
});
for (const [language, expected] of Object.entries({ ru: 'Повторить', en: 'Try again', de: 'Erneut versuchen', es: 'Reintentar', fr: 'Réessayer', uk: 'Спробувати ще раз', pt: 'Tentar novamente', tr: 'Tekrar dene', zh: '重试' })) await test(`localized retry ${language}`, async () => {
  const f = await fixture({ language: 'ru', sdkLanguage: language });
  assert.equal(f.nodes.get('cars-retry-btn')?.textContent, expected);
  const retry = f.nodes.get('cars-retry'); f.context.unityConfig.showBanner('second error', 'error');
  assert.equal(f.nodes.get('cars-retry'), retry, 'Repeated reporting reuses the overlay');
  assert.equal(f.nodes.get('cars-root').children.filter(n => n.id === 'cars-retry').length, 1);
});
await test('browser locale before SDK / regional locale / unknown fallback', async () => {
  for (const [language, expected] of [['zh-CN', '重试'], ['ru-RU', 'Повторить'], ['xx', 'Try again']]) {
    const f = await fixture({ language, failure: 'sdk-missing' }); assert.equal(f.nodes.get('cars-retry-btn')?.textContent, expected);
  }
});
await test('successful boot attaches Unity without retry', async () => {
  const f = await fixture({ failure: 'none' }); assert(f.context.unityInstance); assert.equal(f.starts, 1);
  assert(!f.nodes.get('cars-retry')); assert.equal(f.nodes.get('cars-loading').style.display, 'none'); assert.equal(f.timers.size, 0);
});
await test('nonfatal warning and post-start banner do not replace a running game', async () => {
  const f = await fixture({ failure: 'warning' }); assert(f.context.unityInstance); assert(!f.nodes.get('cars-retry'));
  f.context.unityConfig.showBanner('Runtime diagnostic', 'error'); assert(!f.nodes.get('cars-retry'));
});
if (process.env.OUT) { await fs.mkdir(process.env.OUT, { recursive: true }); await fs.writeFile(`${process.env.OUT}/evidence.json`, JSON.stringify(cases, null, 2)); }
assert(cases.every(c => c.passed), 'Boot recovery regression suite failed');
